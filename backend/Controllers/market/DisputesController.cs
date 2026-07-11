using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public class DisputesController : ControllerBase
{
    private static readonly string[] InProgressStatuses = ["Open", "NeedSupplement"];

    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;

    public DisputesController(AppDbContext db, ICreditService creditService)
    {
        _db = db;
        _creditService = creditService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DisputeTicketResponse>>> GetDisputes()
    {
        var userId = CurrentUserId();
        var canViewAll = IsPlatformArbitrator();

        var query = DisputeQuery();

        if (!canViewAll)
        {
            query = query.Where(d =>
                d.UserID == userId ||
                d.ArbitratorID == userId ||
                (d.Transaction != null && d.Transaction.UserID == userId) ||
                (d.Transaction != null && d.Transaction.Product != null && d.Transaction.Product.UserID == userId));
        }

        var disputes = await query.OrderByDescending(d => d.CreateTime).ToListAsync();
        return Ok(disputes.Select(MapDispute).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DisputeTicketResponse>> GetDispute(int id)
    {
        var dispute = await DisputeQuery().FirstOrDefaultAsync(d => d.TicketID == id);
        if (dispute == null)
            return NotFound(new { message = "纠纷不存在" });

        if (!CanViewDispute(dispute, CurrentUserId()))
            return Forbid();

        return Ok(MapDispute(dispute));
    }

    [HttpPost("transactions/{transactionId}")]
    public async Task<ActionResult<DisputeTicketResponse>> CreateDispute(int transactionId, [FromBody] CreateDisputeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = CurrentUserId();
        var order = await _db.Transactions
            .Include(t => t.User)
            .Include(t => t.Product)
            .ThenInclude(p => p!.User)
            .FirstOrDefaultAsync(t => t.TransactionID == transactionId);

        if (order == null)
            return NotFound(new { message = "订单不存在" });

        if (order.UserID == null || order.Product?.UserID == null)
            return BadRequest(new { message = "订单买家或卖家不存在" });

        var buyerId = order.UserID.Value;
        var sellerId = order.Product.UserID.Value;
        var isBuyer = buyerId == userId;
        var isSeller = sellerId == userId;
        if (!isBuyer && !isSeller)
            return Forbid();

        if (order.TransactionStatus != "Paid")
            return BadRequest(new { message = "只有已支付且未完成的订单可以发起纠纷" });

        var exists = await _db.DisputeTickets.CountAsync(d =>
            d.TransactionID == transactionId &&
            (d.Status == "Open" || d.Status == "NeedSupplement")) > 0;
        if (exists)
            return BadRequest(new { message = "该订单已有处理中的纠纷" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        var arbitratorId = await PickArbitratorAsync(buyerId, sellerId);
        if (!arbitratorId.HasValue)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "暂无可用仲裁员，无法创建纠纷" });
        }

        var disputed = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = 'Disputed'
            WHERE ""transactionId"" = {transactionId}
              AND ""transactionStatus"" = 'Paid'");

        if (disputed != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "只有已支付且未完成的订单可以发起纠纷" });
        }

        order.TransactionStatus = "Disputed";
        var now = DateTime.Now;
        var dispute = new DisputeTicket
        {
            TransactionID = transactionId,
            UserID = userId,
            ArbitratorID = arbitratorId,
            Reason = request.Reason,
            Status = "Open",
            CreateTime = now,
            AssignTime = now
        };

        _db.DisputeTickets.Add(dispute);
        await CreateNotificationAsync(arbitratorId.Value, "纠纷工单已分配", $"订单 {transactionId} 的纠纷已分配给你处理", transactionId);
        await CreateNotificationAsync(buyerId, "订单进入纠纷处理", $"订单 {transactionId} 已进入纠纷处理，仲裁员将尽快处理", transactionId);
        if (sellerId != buyerId)
            await CreateNotificationAsync(sellerId, "订单进入纠纷处理", $"订单 {transactionId} 已进入纠纷处理，仲裁员将尽快处理", transactionId);

        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        dispute.User = await _db.Users.FindAsync(userId);
        dispute.Arbitrator = await _db.Users.FindAsync(arbitratorId.Value);
        dispute.Transaction = order;
        return Ok(MapDispute(dispute));
    }

    [HttpPost("{id}/supplement")]
    public async Task<ActionResult> RequestSupplement(int id, [FromBody] RequestSupplementRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var dispute = await DisputeQuery().FirstOrDefaultAsync(d => d.TicketID == id);
        if (dispute == null)
            return NotFound(new { message = "纠纷不存在" });

        var userId = CurrentUserId();
        if (!IsPlatformArbitrator() || dispute.ArbitratorID != userId)
            return Forbid();

        if (dispute.Status == null || !InProgressStatuses.Contains(dispute.Status))
            return BadRequest(new { message = "该纠纷已处理，不能再要求补充材料" });

        if (dispute.Transaction == null || dispute.Transaction.UserID == null || dispute.Transaction.Product?.UserID == null)
            return BadRequest(new { message = "订单买家或卖家不存在" });

        dispute.Status = "NeedSupplement";
        var content = $"订单 {dispute.TransactionID} 的仲裁员要求补充材料：{request.Message}";
        await CreateNotificationAsync(dispute.Transaction.UserID.Value, "纠纷需补充材料", content, dispute.Transaction.TransactionID);
        var sellerId = dispute.Transaction.Product.UserID.Value;
        if (sellerId != dispute.Transaction.UserID.Value)
            await CreateNotificationAsync(sellerId, "纠纷需补充材料", content, dispute.Transaction.TransactionID);

        await _db.SaveChangesAsync();
        return Ok(new { message = "已通知买卖双方补充材料", status = dispute.Status });
    }

    [HttpPost("{id}/resolve")]
    public async Task<ActionResult> Resolve(int id, [FromBody] ResolveDisputeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var dispute = await DisputeQuery().FirstOrDefaultAsync(d => d.TicketID == id);
        if (dispute == null)
            return NotFound(new { message = "纠纷不存在" });

        var currentUserId = CurrentUserId();
        if (!IsPlatformArbitrator() || dispute.ArbitratorID != currentUserId)
            return Forbid();

        if (dispute.Status == null || !InProgressStatuses.Contains(dispute.Status))
            return BadRequest(new { message = "该纠纷已处理" });
        if (dispute.Transaction == null || dispute.Transaction.UserID == null || dispute.Transaction.Product?.UserID == null)
            return BadRequest(new { message = "订单或卖家不存在" });
        if (dispute.Transaction.TransactionStatus != "Disputed")
            return BadRequest(new { message = "订单当前不在纠纷处理中" });

        var buyerId = dispute.Transaction.UserID.Value;
        var sellerId = dispute.Transaction.Product.UserID.Value;
        if (currentUserId == buyerId || currentUserId == sellerId)
            return Forbid();

        var amount = Math.Round(dispute.Transaction.TransactionAmount ?? 0, 2, MidpointRounding.AwayFromZero);
        var refundAmount = Math.Round(request.RefundAmount, 2, MidpointRounding.AwayFromZero);
        if (refundAmount < 0)
            return BadRequest(new { message = "退款金额不能为负数" });
        if (refundAmount > amount)
            return BadRequest(new { message = "退款金额不能超过订单金额" });

        var sellerAmount = amount - refundAmount;
        if (refundAmount + sellerAmount != amount)
            return BadRequest(new { message = "退款拆分金额不正确" });

        var responsibilityParty = NormalizeResponsibilityParty(request.ResponsibilityParty)
            ?? InferResponsibilityParty(amount, refundAmount);
        if (string.IsNullOrEmpty(responsibilityParty))
            return BadRequest(new { message = "责任方必须是 Buyer、Seller、Both 或 None" });
        var responsibilityText = ResponsibilityPartyText(responsibilityParty);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var finalStatus = refundAmount > 0 ? "Refunded" : "Completed";
        var disputeUpdated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""DisputeTicket""
            SET ""status"" = 'Resolved'
            WHERE ""ticketId"" = {id}
              AND ""arbitratorId"" = {currentUserId}
              AND ""status"" IN ('Open', 'NeedSupplement')");

        var orderUpdated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = {finalStatus}
            WHERE ""transactionId"" = {dispute.Transaction.TransactionID}
              AND ""transactionStatus"" = 'Disputed'");

        if (disputeUpdated != 1 || orderUpdated != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "该纠纷或订单状态已变化，不能重复结算" });
        }

        var buyerWallet = await GetOrCreateWalletAsync(buyerId);
        var sellerWallet = await GetOrCreateWalletAsync(sellerId);
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" + {refundAmount}
            WHERE ""walletId"" = {buyerWallet.WalletID}");
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" + {sellerAmount}
            WHERE ""walletId"" = {sellerWallet.WalletID}");

        if (dispute.Transaction.ProductID.HasValue)
        {
            if (refundAmount >= amount)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE ""Product""
                    SET ""stock"" = ""stock"" + 1,
                        ""status"" = 'Active'
                    WHERE ""productId"" = {dispute.Transaction.ProductID.Value}");
                dispute.Transaction.Product.Stock = (dispute.Transaction.Product.Stock ?? 0) + 1;
                dispute.Transaction.Product.Status = "Active";
            }
            else if (dispute.Transaction.Product != null && (dispute.Transaction.Product.Stock ?? 0) <= 0)
            {
                dispute.Transaction.Product.Status = "Sold";
            }
        }

        dispute.Status = "Resolved";
        dispute.Transaction.TransactionStatus = finalStatus;
        await ArchiveOrderMessagesAsync(dispute.Transaction.TransactionID);

        var result = new ArbitrationResult
        {
            DisputeTicketID = id,
            Decision = $"{request.Decision}\n责任方：{responsibilityText}",
            RefundAmount = refundAmount,
            CreateTime = DateTime.Now,
            WalletID = buyerWallet.WalletID > 0 ? buyerWallet.WalletID : null
        };
        _db.ArbitrationResults.Add(result);

        await CreateNotificationAsync(buyerId, "纠纷处理完成", $"订单 {dispute.TransactionID} 退款 ¥{refundAmount:F2}，责任方：{responsibilityText}", dispute.Transaction.TransactionID);
        await CreateNotificationAsync(sellerId, "纠纷处理完成", $"订单 {dispute.TransactionID} 结算 ¥{sellerAmount:F2}，责任方：{responsibilityText}", dispute.Transaction.TransactionID);
        await ApplyDisputeCreditImpactAsync(dispute, buyerId, sellerId, responsibilityParty);
        await _db.SaveChangesAsync();
        await _db.Entry(buyerWallet).ReloadAsync();
        await _db.Entry(sellerWallet).ReloadAsync();
        await dbTransaction.CommitAsync();

        return Ok(new
        {
            message = "纠纷已处理",
            status = dispute.Status,
            orderStatus = finalStatus,
            buyerRefundAmount = refundAmount,
            sellerSettlementAmount = sellerAmount,
            responsibilityParty = responsibilityParty,
            responsibilityPartyText = responsibilityText,
            buyerWalletBalance = buyerWallet.Balance ?? 0,
            sellerWalletBalance = sellerWallet.Balance ?? 0
        });
    }

    private IQueryable<DisputeTicket> DisputeQuery()
    {
        return _db.DisputeTickets
            .Include(d => d.User)
            .Include(d => d.Arbitrator)
            .Include(d => d.ArbitrationResults)
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.User)
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.Product)
            .ThenInclude(p => p!.User);
    }

    private async Task ApplyDisputeCreditImpactAsync(DisputeTicket dispute, int buyerId, int sellerId, string responsibilityParty)
    {
        var orderLabel = $"订单 {dispute.TransactionID}";
        switch (responsibilityParty)
        {
            case "Seller":
                await _creditService.AddCreditAsync(sellerId, -20, $"纠纷仲裁：{orderLabel} 卖家责任");
                break;
            case "Buyer":
                await _creditService.AddCreditAsync(buyerId, -10, $"纠纷仲裁：{orderLabel} 买家责任");
                break;
            case "Both":
                await _creditService.AddCreditAsync(sellerId, -10, $"纠纷仲裁：{orderLabel} 双方责任，卖家部分责任");
                await _creditService.AddCreditAsync(buyerId, -5, $"纠纷仲裁：{orderLabel} 双方责任，买家部分责任");
                break;
            case "None":
                // 仲裁认定无责任方时不扣信用分。
                break;
        }
    }

    private static string? NormalizeResponsibilityParty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "buyer" or "买家" or "买家责任" => "Buyer",
            "seller" or "卖家" or "卖家责任" => "Seller",
            "both" or "all" or "双方" or "双方责任" => "Both",
            "none" or "no" or "无" or "无责任" => "None",
            _ => string.Empty
        };
    }

    private static string? InferResponsibilityParty(decimal amount, decimal refundAmount)
    {
        if (amount <= 0)
            return "None";
        if (refundAmount >= amount)
            return "Seller";
        if (refundAmount <= 0)
            return "Buyer";
        return "Both";
    }

    private static string ResponsibilityPartyText(string responsibilityParty)
    {
        return responsibilityParty switch
        {
            "Buyer" => "买家责任",
            "Seller" => "卖家责任",
            "Both" => "双方责任",
            "None" => "无责任",
            _ => responsibilityParty
        };
    }

    private async Task<int?> PickArbitratorAsync(int buyerId, int sellerId)
    {
        var excluded = new[] { buyerId, sellerId };
        var candidates = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => !excluded.Contains(ur.UserID) &&
                ur.Role != null &&
                ur.Role.RoleName == "Moderator")
            .Select(ur => ur.UserID)
            .Distinct()
            .ToListAsync();

        if (candidates.Count == 0)
            return null;

        var openCounts = await _db.DisputeTickets
            .Where(d => (d.Status == "Open" || d.Status == "NeedSupplement") && d.ArbitratorID.HasValue)
            .GroupBy(d => d.ArbitratorID!.Value)
            .Select(g => new { ArbitratorID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ArbitratorID, x => x.Count);

        return candidates
            .OrderBy(id => openCounts.TryGetValue(id, out var count) ? count : 0)
            .ThenBy(id => id)
            .First();
    }

    private bool CanViewDispute(DisputeTicket dispute, int userId)
    {
        if (IsPlatformArbitrator())
            return true;

        var buyerId = dispute.Transaction?.UserID;
        var sellerId = dispute.Transaction?.Product?.UserID;
        return dispute.UserID == userId || dispute.ArbitratorID == userId || buyerId == userId || sellerId == userId;
    }

    private bool IsPlatformArbitrator()
    {
        return User.IsInRole("Moderator") ||
            HasAnyPermission("disputes.view", "disputes.resolve");
    }

    private bool HasAnyPermission(params string[] permissions)
    {
        var userPermissions = User.Claims
            .Where(c => c.Type == "Permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return permissions.Any(userPermissions.Contains);
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

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

    private async Task CreateNotificationAsync(int userId, string title, string content, int transactionId)
    {
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Title = title,
            Content = content,
            TransactionID = transactionId,
            CreateTime = DateTime.Now
        });
        await Task.CompletedTask;
    }

    private async Task ArchiveOrderMessagesAsync(int transactionId)
    {
        var messages = await _db.OrderMessages
            .Where(m => m.TransactionID == transactionId && m.IsArchived != "1")
            .ToListAsync();

        foreach (var message in messages)
            message.IsArchived = "1";
    }

    private static DisputeTicketResponse MapDispute(DisputeTicket dispute)
    {
        var result = dispute.ArbitrationResults
            .OrderByDescending(r => r.CreateTime)
            .FirstOrDefault();
        var amount = dispute.Transaction?.TransactionAmount ?? 0;
        var refundAmount = result?.RefundAmount;

        return new DisputeTicketResponse
        {
            TicketID = dispute.TicketID,
            Reason = dispute.Reason ?? "",
            Status = dispute.Status ?? "",
            CreateTime = dispute.CreateTime,
            AssignTime = dispute.AssignTime,
            TransactionID = dispute.TransactionID,
            TransactionAmount = amount,
            OrderStatus = dispute.Transaction?.TransactionStatus ?? "",
            ProductTitle = dispute.Transaction?.Product?.Title ?? "",
            BuyerID = dispute.Transaction?.UserID,
            BuyerName = dispute.Transaction?.User?.Username ?? "",
            SellerID = dispute.Transaction?.Product?.UserID,
            SellerName = dispute.Transaction?.Product?.User?.Username ?? "",
            UserID = dispute.UserID,
            Username = dispute.User?.Username ?? "",
            ArbitratorID = dispute.ArbitratorID,
            ArbitratorName = dispute.Arbitrator?.Username ?? "",
            Decision = result?.Decision ?? "",
            RefundAmount = refundAmount,
            SellerSettlementAmount = refundAmount.HasValue ? amount - refundAmount.Value : null,
            ResolvedTime = result?.CreateTime
        };
    }
}
