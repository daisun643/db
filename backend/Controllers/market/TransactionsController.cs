using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Authorization;
using Backend.Services;
using Backend.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly INotificationService _notificationService;

    public TransactionsController(AppDbContext db, ICreditService creditService, INotificationService notificationService)
    {
        _db = db;
        _creditService = creditService;
        _notificationService = notificationService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<List<TransactionResponse>>> GetMyOrders(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var normalizedStatus = status?.Trim();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        // 排除钱包充值流水（无商品关联），只返回商品订单
        var orders = _db.Transactions
            .Include(t => t.Product)
            .ThenInclude(p => p!.User)
            .Include(t => t.User)
            .Where(t => t.UserID == userId && t.ProductID != null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            var normalized = NormalizeTransactionStatus(normalizedStatus);
            if (normalized == null)
                return BadRequest(new { message = "订单状态不合法" });
            orders = orders.Where(t => t.TransactionStatus == normalized);
        }

        orders = orders
            .OrderByDescending(t => t.CreateTime)
            .ThenByDescending(t => t.TransactionID);

        var totalCount = await orders.CountAsync();
        var items = await orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers["X-Total-Count"] = totalCount.ToString();

        return Ok(items.Select(MapTransaction).ToList());
    }

    [HttpGet("sales")]
    public async Task<ActionResult<List<TransactionResponse>>> GetSales(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var normalizedStatus = status?.Trim();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var orders = _db.Transactions
            .Include(t => t.Product)
            .ThenInclude(p => p!.User)
            .Include(t => t.User)
            .Where(t => t.Product != null && t.Product.UserID == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            var normalized = NormalizeTransactionStatus(normalizedStatus);
            if (normalized == null)
                return BadRequest(new { message = "订单状态不合法" });
            orders = orders.Where(t => t.TransactionStatus == normalized);
        }

        orders = orders
            .OrderByDescending(t => t.CreateTime)
            .ThenByDescending(t => t.TransactionID);

        var totalCount = await orders.CountAsync();
        var items = await orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers["X-Total-Count"] = totalCount.ToString();

        return Ok(items.Select(MapTransaction).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> CreateOrder([FromBody] CreateTransactionRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!await _creditService.CanPerformAsync(userId, "order.create"))
            return BadRequest(new { message = "信用分不足或账号不可用，暂不能接单/下单" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        // 商品校验、条件扣减库存与建单全部在存储过程内完成；
        // 库存归零后由 TRG_Product_StockLock 自动把商品置为 Locked
        var code = OracleProcedure.OutInt32("p_code");
        var message = OracleProcedure.OutText("p_message");
        var transactionId = OracleProcedure.OutInt32("p_transaction_id");
        var productTitle = OracleProcedure.OutText("p_product_title");
        var sellerId = OracleProcedure.OutInt32("p_seller_id");

        await OracleProcedure.CallAsync(_db, "sp_create_order",
            OracleProcedure.InInt32("p_buyer_id", userId),
            OracleProcedure.InInt32("p_product_id", request.ProductID),
            code,
            message,
            transactionId,
            OracleProcedure.OutDecimal("p_amount"),
            productTitle,
            sellerId);

        if (OracleProcedure.ReadInt32(code) != OracleProcedure.Success)
            return BadRequest(new { message = OracleProcedure.ReadText(message) ?? "商品不可下单" });

        var orderId = OracleProcedure.ReadInt32(transactionId);
        var title = OracleProcedure.ReadText(productTitle) ?? "";

        await CreateNotificationAsync(userId, "订单已创建", $"你已锁定商品：{title}", orderId);
        var seller = OracleProcedure.ReadInt32OrNull(sellerId);
        if (seller.HasValue)
            await CreateNotificationAsync(seller.Value, "商品被下单", $"商品 {title} 已被买家锁定", orderId);

        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        var order = await _db.Transactions
            .Include(t => t.Product)
            .ThenInclude(p => p!.User)
            .Include(t => t.User)
            .AsNoTracking()
            .FirstAsync(t => t.TransactionID == orderId);

        return Ok(MapTransaction(order));
    }

    [HttpPost("{id}/pay")]
    public async Task<ActionResult> Pay(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        // 余额条件扣款与 Pending → Paid 的状态机在存储过程内原子完成
        var code = OracleProcedure.OutInt32("p_code");
        var message = OracleProcedure.OutText("p_message");
        var balance = OracleProcedure.OutDecimal("p_balance");
        var sellerId = OracleProcedure.OutInt32("p_seller_id");

        await OracleProcedure.CallAsync(_db, "sp_pay_order",
            OracleProcedure.InInt32("p_transaction_id", id),
            OracleProcedure.InInt32("p_buyer_id", order.UserID),
            code,
            message,
            balance,
            OracleProcedure.OutDecimal("p_amount"),
            sellerId);

        if (OracleProcedure.ReadInt32(code) != OracleProcedure.Success)
            return BadRequest(new { message = OracleProcedure.ReadText(message) ?? "当前订单不可支付" });

        await CreateNotificationAsync(order.UserID!.Value, "支付成功", $"订单 {id} 已支付，等待确认收货", id);
        var seller = OracleProcedure.ReadInt32OrNull(sellerId);
        if (seller.HasValue)
            await CreateNotificationAsync(seller.Value, "买家已支付", $"订单 {id} 已支付", id);

        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return Ok(new
        {
            message = "支付成功，资金已进入担保账户",
            status = "Paid",
            walletBalance = OracleProcedure.ReadDecimal(balance)
        });
    }

    [HttpPost("{id}/confirm-receipt")]
    public async Task<ActionResult> ConfirmReceipt(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        // Paid → Completed、卖家钱包结算、售罄转 Sold 都在存储过程内完成；
        // 留言板归档由 TRG_Transaction_ArchiveMsg 自动处理
        var code = OracleProcedure.OutInt32("p_code");
        var message = OracleProcedure.OutText("p_message");
        var sellerId = OracleProcedure.OutInt32("p_seller_id");

        await OracleProcedure.CallAsync(_db, "sp_confirm_receipt",
            OracleProcedure.InInt32("p_transaction_id", id),
            code,
            message,
            OracleProcedure.OutDecimal("p_amount"),
            sellerId,
            OracleProcedure.OutDecimal("p_seller_balance"));

        if (OracleProcedure.ReadInt32(code) != OracleProcedure.Success)
            return BadRequest(new { message = OracleProcedure.ReadText(message) ?? "当前订单不可确认收货" });

        await CreateNotificationAsync(order.UserID!.Value, "交易完成", $"订单 {id} 已完成", id);
        var seller = OracleProcedure.ReadInt32OrNull(sellerId);
        if (seller.HasValue)
            await CreateNotificationAsync(seller.Value, "交易完成", $"订单 {id} 已完成，可结算资金", id);

        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return Ok(new { message = "确认收货成功，资金已结算给卖家", status = "Completed" });
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        // Pending → Cancelled 与库存回补在存储过程内原子完成
        var code = OracleProcedure.OutInt32("p_code");
        var message = OracleProcedure.OutText("p_message");

        await OracleProcedure.CallAsync(_db, "sp_cancel_order",
            OracleProcedure.InInt32("p_transaction_id", id),
            code,
            message);

        if (OracleProcedure.ReadInt32(code) != OracleProcedure.Success)
            return BadRequest(new { message = OracleProcedure.ReadText(message) ?? "只有待支付订单可以取消" });

        await dbTransaction.CommitAsync();

        return Ok(new { message = "订单已取消", status = "Cancelled" });
    }

    [HttpGet("{id}/messages")]
    public async Task<ActionResult<List<OrderMessageResponse>>> GetOrderMessages(int id)
    {
        var order = await FindParticipatingOrderAsync(id);
        if (order == null)
            return NotFound();

        var messages = await _db.OrderMessages
            .Include(m => m.Sender)
            .Where(m => m.TransactionID == id)
            .OrderBy(m => m.SendTime)
            .ToListAsync();

        return Ok(messages.Select(MapOrderMessage).ToList());
    }

    [HttpPost("{id}/messages")]
    public async Task<ActionResult<OrderMessageResponse>> SendOrderMessage(int id, [FromBody] CreateOrderMessageRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var order = await FindParticipatingOrderAsync(id);
        if (order == null)
            return NotFound();

        if (IsArchivedStatus(order.TransactionStatus))
            return BadRequest(new { message = "交易已结束，留言板已归档只读" });

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var message = new OrderMessage
        {
            TransactionID = id,
            SenderID = userId,
            Content = request.Content,
            SendTime = DateTime.Now,
            IsArchived = "0"
        };

        _db.OrderMessages.Add(message);
        await _db.SaveChangesAsync();

        message.Sender = await _db.Users.FindAsync(userId);
        return Ok(MapOrderMessage(message));
    }

    private async Task<Transaction?> FindOwnedOrderAsync(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        return await _db.Transactions
            .Include(t => t.Product)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TransactionID == id && t.UserID == userId);
    }

    private async Task<Transaction?> FindParticipatingOrderAsync(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        return await _db.Transactions
            .Include(t => t.Product)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TransactionID == id &&
                (t.UserID == userId || (t.Product != null && t.Product.UserID == userId)));
    }

    private async Task CreateNotificationAsync(int userId, string title, string content, int transactionId)
    {
        await _notificationService.CreateAsync(new CreateNotificationOptions
        {
            UserID = userId,
            Type = "Transaction",
            Title = title,
            Content = content,
            TargetType = "Transaction",
            TargetID = transactionId,
            TransactionID = transactionId,
            Link = $"/products",
            EventKey = $"transaction:{transactionId}:{userId}:{title}"
        });
    }

    private static TransactionResponse MapTransaction(Transaction transaction)
    {
        return new TransactionResponse
        {
            TransactionID = transaction.TransactionID,
            TransactionAmount = transaction.TransactionAmount ?? 0,
            TransactionStatus = transaction.TransactionStatus ?? "",
            CreateTime = transaction.CreateTime,
            PayTime = transaction.PayTime,
            UserID = transaction.UserID,
            ProductID = transaction.ProductID,
            ProductTitle = transaction.Product?.Title ?? "",
            BuyerName = transaction.User?.Username ?? "",
            SellerID = transaction.Product?.UserID,
            SellerName = transaction.Product?.User?.Username ?? ""
        };
    }

    private static bool IsArchivedStatus(string? status)
    {
        return status is "Completed" or "Cancelled" or "Refunded";
    }

    private static string? NormalizeTransactionStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "paid" => "Paid",
            "completed" => "Completed",
            "cancelled" => "Cancelled",
            "disputed" => "Disputed",
            "refunded" => "Refunded",
            _ => null,
        };
    }

    private static OrderMessageResponse MapOrderMessage(OrderMessage message)
    {
        return new OrderMessageResponse
        {
            OrderMessageID = message.OrderMessageID,
            Content = message.Content ?? "",
            SendTime = message.SendTime,
            IsArchived = message.IsArchived == "1",
            TransactionID = message.TransactionID,
            SenderID = message.SenderID,
            SenderName = message.Sender?.Username ?? ""
        };
    }
}
