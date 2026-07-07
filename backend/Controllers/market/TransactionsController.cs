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
    public async Task<ActionResult<List<TransactionResponse>>> GetMyOrders()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var orders = await _db.Transactions
            .Include(t => t.Product)
            .ThenInclude(p => p!.User)
            .Include(t => t.User)
            .Where(t => t.UserID == userId)
            .OrderByDescending(t => t.CreateTime)
            .ToListAsync();

        return Ok(orders.Select(MapTransaction).ToList());
    }

    [HttpGet("sales")]
    public async Task<ActionResult<List<TransactionResponse>>> GetSales()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var orders = await _db.Transactions
            .Include(t => t.Product)
            .ThenInclude(p => p!.User)
            .Include(t => t.User)
            .Where(t => t.Product != null && t.Product.UserID == userId)
            .OrderByDescending(t => t.CreateTime)
            .ToListAsync();

        return Ok(orders.Select(MapTransaction).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> CreateOrder([FromBody] CreateTransactionRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!await _creditService.CanPerformAsync(userId, "order.create"))
            return BadRequest(new { message = "信用分不足或账号不可用，暂不能接单/下单" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        var product = await _db.Products
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.ProductID == request.ProductID);
        if (product == null || product.Status != "Active")
            return BadRequest(new { message = "商品不可下单" });

        if (product.UserID == userId)
            return BadRequest(new { message = "不能购买自己发布的商品" });

        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Product""
            SET ""stock"" = ""stock"" - 1,
                ""status"" = CASE WHEN ""stock"" - 1 <= 0 THEN 'Locked' ELSE ""status"" END
            WHERE ""productId"" = {request.ProductID}
              AND ""stock"" > 0
              AND ""status"" = 'Active'");

        if (affected != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "库存不足或商品已锁定" });
        }

        var order = new Transaction
        {
            TransactionAmount = product.Price ?? 0,
            TransactionStatus = "Pending",
            CreateTime = DateTime.Now,
            UserID = userId,
            ProductID = request.ProductID
        };

        _db.Transactions.Add(order);
        await _db.SaveChangesAsync();

        await CreateNotificationAsync(userId, "订单已创建", $"你已锁定商品：{product.Title}", order.TransactionID);
        if (product.UserID.HasValue)
            await CreateNotificationAsync(product.UserID.Value, "商品被下单", $"商品 {product.Title} 已被买家锁定", order.TransactionID);

        await _db.SaveChangesAsync();

        await dbTransaction.CommitAsync();

        order.Product = product;
        order.User = await _db.Users.FindAsync(userId);
        return Ok(MapTransaction(order));
    }

    [HttpPost("{id}/pay")]
    public async Task<ActionResult> Pay(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();
        if (order.TransactionStatus != "Pending")
            return BadRequest(new { message = "当前订单不可支付" });
        if (order.Product?.UserID == null)
            return BadRequest(new { message = "商品卖家不存在" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var buyerWallet = await GetOrCreateWalletAsync(order.UserID!.Value);
        await _db.SaveChangesAsync();

        var amount = order.TransactionAmount ?? 0;
        var debited = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" - {amount}
            WHERE ""walletId"" = {buyerWallet.WalletID}
              AND ""balance"" >= {amount}");

        if (debited != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "钱包余额不足" });
        }

        var payTime = DateTime.Now;
        var paid = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = 'Paid',
                ""payTime"" = {payTime}
            WHERE ""transactionId"" = {id}
              AND ""transactionStatus"" = 'Pending'");

        if (paid != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "当前订单不可支付" });
        }

        order.TransactionStatus = "Paid";
        order.PayTime = payTime;
        await CreateNotificationAsync(order.UserID.Value, "支付成功", $"订单 {id} 已支付，等待确认收货", id);
        if (order.Product?.UserID.HasValue == true)
            await CreateNotificationAsync(order.Product.UserID.Value, "买家已支付", $"订单 {id} 已支付", id);
        await _db.SaveChangesAsync();
        await _db.Entry(buyerWallet).ReloadAsync();
        await dbTransaction.CommitAsync();

        return Ok(new { message = "支付成功，资金已进入担保账户", status = order.TransactionStatus, walletBalance = buyerWallet.Balance ?? 0 });
    }

    [HttpPost("{id}/confirm-receipt")]
    public async Task<ActionResult> ConfirmReceipt(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();
        if (order.TransactionStatus != "Paid")
            return BadRequest(new { message = "当前订单不可确认收货" });
        if (order.Product?.UserID == null)
            return BadRequest(new { message = "商品卖家不存在" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var sellerWallet = await GetOrCreateWalletAsync(order.Product.UserID.Value);
        await _db.SaveChangesAsync();

        var completed = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = 'Completed'
            WHERE ""transactionId"" = {id}
              AND ""transactionStatus"" = 'Paid'");

        if (completed != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "当前订单不可确认收货" });
        }

        var amount = order.TransactionAmount ?? 0;
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" + {amount}
            WHERE ""walletId"" = {sellerWallet.WalletID}");

        order.TransactionStatus = "Completed";
        if (order.Product != null && (order.Product.Stock ?? 0) <= 0)
            order.Product.Status = "Sold";
        await ArchiveOrderMessagesAsync(id);

        await CreateNotificationAsync(order.UserID!.Value, "交易完成", $"订单 {id} 已完成", id);
        if (order.Product?.UserID.HasValue == true)
            await CreateNotificationAsync(order.Product.UserID.Value, "交易完成", $"订单 {id} 已完成，可结算资金", id);

        await _db.SaveChangesAsync();
        await _db.Entry(sellerWallet).ReloadAsync();
        await dbTransaction.CommitAsync();
        return Ok(new { message = "确认收货成功，资金已结算给卖家", status = order.TransactionStatus });
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();
        if (order.TransactionStatus != "Pending")
            return BadRequest(new { message = "只有待支付订单可以取消" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var cancelled = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = 'Cancelled'
            WHERE ""transactionId"" = {id}
              AND ""transactionStatus"" = 'Pending'");

        if (cancelled != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "只有待支付订单可以取消" });
        }

        order.TransactionStatus = "Cancelled";
        if (order.ProductID.HasValue)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Product""
                SET ""stock"" = ""stock"" + 1,
                    ""status"" = 'Active'
                WHERE ""productId"" = {order.ProductID.Value}");
        }
        await ArchiveOrderMessagesAsync(id);
        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return Ok(new { message = "订单已取消", status = order.TransactionStatus });
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
            Link = $"/products/orders/{transactionId}",
            EventKey = $"transaction:{transactionId}:{userId}:{title}"
        });
    }

    private async Task ArchiveOrderMessagesAsync(int transactionId)
    {
        var messages = await _db.OrderMessages
            .Where(m => m.TransactionID == transactionId && m.IsArchived != "1")
            .ToListAsync();

        foreach (var message in messages)
            message.IsArchived = "1";
    }

    private async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserID == userId);
        if (wallet != null)
            return wallet;

        wallet = new Wallet
        {
            UserID = userId,
            Balance = 0
        };
        _db.Wallets.Add(wallet);
        return wallet;
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


