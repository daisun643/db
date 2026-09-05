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
[Route("api/disputes")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly INotificationService _notificationService;

    public DisputesController(AppDbContext db, ICreditService creditService, INotificationService notificationService)
    {
        _db = db;
        _creditService = creditService;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DisputeTicketResponse>>> GetDisputes()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var canViewAll = User.IsInRole("Manager") ||
            User.IsInRole("Moderator") ||
            HasAnyPermission("dashboard.view", "products.edit");

        var query = _db.DisputeTickets
            .Include(d => d.User)
            .Include(d => d.Arbitrator)
            .Include(d => d.ArbitrationResults)
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.User)
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.Product)
            .ThenInclude(p => p!.User)
            .AsQueryable();

        if (!canViewAll)
            query = query.Where(d => d.UserID == userId || d.ArbitratorID == userId);

        var disputes = await query.OrderByDescending(d => d.CreateTime).ToListAsync();
        return Ok(disputes.Select(MapDispute).ToList());
    }

    [HttpPost("transactions/{transactionId}")]
    public async Task<ActionResult<DisputeTicketResponse>> CreateDispute(int transactionId, [FromBody] CreateDisputeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var order = await _db.Transactions
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.TransactionID == transactionId);

        if (order == null)
            return NotFound(new { message = "订单不存在" });

        var isBuyer = order.UserID == userId;
        var isSeller = order.Product?.UserID == userId;
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

        var arbitratorId = await PickArbitratorAsync(order.UserID, order.Product?.UserID);
        order.TransactionStatus = "Disputed";
        var dispute = new DisputeTicket
        {
            TransactionID = transactionId,
            UserID = userId,
            ArbitratorID = arbitratorId,
            Reason = request.Reason,
            Status = "Open",
            CreateTime = DateTime.Now,
            AssignTime = arbitratorId.HasValue ? DateTime.Now : null
        };

        _db.DisputeTickets.Add(dispute);
        if (arbitratorId.HasValue)
            await CreateNotificationAsync(arbitratorId.Value, "纠纷工单已分配", $"订单 {transactionId} 的纠纷已分配给你处理", transactionId);
        if (order.UserID.HasValue && order.UserID.Value != userId)
            await CreateNotificationAsync(order.UserID.Value, "订单纠纷已发起", $"订单 {transactionId} 已进入纠纷处理", transactionId);
        if (order.Product?.UserID.HasValue == true && order.Product.UserID.Value != userId)
            await CreateNotificationAsync(order.Product.UserID.Value, "订单纠纷已发起", $"订单 {transactionId} 已进入纠纷处理", transactionId);
        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        dispute.User = await _db.Users.FindAsync(userId);
        dispute.Arbitrator = arbitratorId.HasValue ? await _db.Users.FindAsync(arbitratorId.Value) : null;
        return Ok(MapDispute(dispute));
    }

    [HttpPost("{id}/resolve")]
    public async Task<ActionResult> Resolve(int id, [FromBody] ResolveDisputeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        var dispute = await _db.DisputeTickets
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.Product)
            .Include(d => d.User)
            .Include(d => d.Arbitrator)
            .Include(d => d.ArbitrationResults)
            .FirstOrDefaultAsync(d => d.TicketID == id);
        if (dispute == null)
            return NotFound();
        if (dispute.Status is not "Open" and not "NeedSupplement")
            return BadRequest(new { message = "该纠纷已处理" });
        // 工单优先分派给版主，而后台“交易纠纷”页面只对站点管理员开放，
        // 因此管理员需要能接管并结案分派给他人的工单。
        if (!User.IsInRole("Manager") &&
            dispute.ArbitratorID != userId &&
            dispute.Transaction?.Product?.UserID != userId)
            return Forbid();
        if (dispute.Transaction == null || dispute.Transaction.Product?.UserID == null)
            return BadRequest(new { message = "订单或卖家不存在" });
        if (dispute.Transaction.TransactionStatus != "Disputed")
            return BadRequest(new { message = "订单当前不在纠纷处理中" });

        var buyerId = dispute.Transaction.UserID!.Value;
        var sellerId = dispute.Transaction.Product.UserID.Value;
        var amount = dispute.Transaction.TransactionAmount ?? 0;
        if (request.RefundAmount > amount)
            return BadRequest(new { message = "退款金额不能超过订单金额" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var finalStatus = request.RefundAmount > 0 ? "Refunded" : "Completed";
        var disputeUpdated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""DisputeTicket""
            SET ""status"" = 'Resolved'
            WHERE ""ticketId"" = {id}
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

        var sellerAmount = amount - request.RefundAmount;
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" + {request.RefundAmount}
            WHERE ""walletId"" = {buyerWallet.WalletID}");
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" + {sellerAmount}
            WHERE ""walletId"" = {sellerWallet.WalletID}");

        dispute.Status = "Resolved";
        dispute.AssignTime = dispute.AssignTime ?? DateTime.Now;
        dispute.Transaction.TransactionStatus = finalStatus;
        if (dispute.Transaction.Product != null && (dispute.Transaction.Product.Stock ?? 0) <= 0)
            dispute.Transaction.Product.Status = "Sold";
        await ArchiveOrderMessagesAsync(dispute.Transaction.TransactionID);

        _db.ArbitrationResults.Add(new ArbitrationResult
        {
            DisputeTicketID = id,
            Decision = request.Decision,
            RefundAmount = request.RefundAmount,
            CreateTime = DateTime.Now,
            WalletID = buyerWallet.WalletID > 0 ? buyerWallet.WalletID : null
        });

        await CreateNotificationAsync(buyerId, "纠纷处理完成", $"订单 {dispute.TransactionID} 退款 ¥{request.RefundAmount}", dispute.Transaction.TransactionID);
        await CreateNotificationAsync(sellerId, "纠纷处理完成", $"订单 {dispute.TransactionID} 结算 ¥{sellerAmount}", dispute.Transaction.TransactionID);
        await ApplyDisputeCreditImpactAsync(dispute, buyerId, sellerId, amount, request.RefundAmount, request.ResponsibilityParty);
        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();
        return Ok(new
        {
            message = "纠纷已处理",
            status = dispute.Status,
            orderStatus = finalStatus,
            buyerRefundAmount = request.RefundAmount,
            sellerSettlementAmount = amount - request.RefundAmount
        });
    }

    [HttpPost("{id}/supplement")]
    public async Task<ActionResult<DisputeTicketResponse>> RequestSupplement(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var dispute = await _db.DisputeTickets
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.User)
            .Include(d => d.Transaction!)
            .ThenInclude(t => t!.Product)
            .ThenInclude(p => p!.User)
            .Include(d => d.ArbitrationResults)
            .FirstOrDefaultAsync(d => d.TicketID == id);

        if (dispute == null)
            return NotFound();
        if (dispute.ArbitratorID != userId)
            return Forbid();
        if (dispute.Status != "Open")
            return BadRequest(new { message = "该纠纷当前不允许提交补充材料" });

        var updated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""DisputeTicket""
            SET ""status"" = 'NeedSupplement'
            WHERE ""ticketId"" = {id}
              AND ""status"" = 'Open'");

        if (updated != 1)
        {
            await _db.SaveChangesAsync();
            return BadRequest(new { message = "该纠纷状态已变化，不能提交补充材料" });
        }

        dispute.Status = "NeedSupplement";
        return Ok(MapDispute(dispute));
    }

    private async Task ApplyDisputeCreditImpactAsync(DisputeTicket dispute, int buyerId, int sellerId, decimal amount, decimal refundAmount, string? responsibilityParty)
    {
        if (amount <= 0)
            return;

        if (string.IsNullOrWhiteSpace(responsibilityParty))
            return;

        var orderLabel = $"订单 {dispute.TransactionID}";
        var normalizedParty = responsibilityParty?.Trim();

        if (string.Equals(normalizedParty, "Seller", StringComparison.OrdinalIgnoreCase))
        {
            await _creditService.AddCreditAsync(sellerId, -20, $"纠纷仲裁：{orderLabel} 全额退款，卖家违约");
            return;
        }

        if (string.Equals(normalizedParty, "Buyer", StringComparison.OrdinalIgnoreCase))
        {
            await _creditService.AddCreditAsync(buyerId, -10, $"纠纷仲裁：{orderLabel} 不予退款，买家责任");
            return;
        }

        if (refundAmount >= amount)
        {
            await _creditService.AddCreditAsync(sellerId, -20, $"纠纷仲裁：{orderLabel} 全额退款，卖家违约");
            return;
        }

        if (refundAmount <= 0)
        {
            await _creditService.AddCreditAsync(buyerId, -10, $"纠纷仲裁：{orderLabel} 不予退款，买家责任");
            return;
        }

        await _creditService.AddCreditAsync(sellerId, -10, $"纠纷仲裁：{orderLabel} 部分退款，卖家部分责任");
        await _creditService.AddCreditAsync(buyerId, -5, $"纠纷仲裁：{orderLabel} 部分退款，买家部分责任");
    }

    private async Task<int?> PickArbitratorAsync(int? buyerId, int? sellerId)
    {
        var candidates = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.Role != null &&
                (ur.Role.RoleName == "Manager" ||
                 ur.Role.RoleName == "Moderator"))
            .Select(ur => ur.UserID)
            .Distinct()
            .ToListAsync();

        var filteredCandidates = candidates
            .Where(id => id != buyerId && id != sellerId)
            .ToList();

        if (filteredCandidates.Count == 0)
            return null;

        var openCounts = await _db.DisputeTickets
            .Where(d => (d.Status == "Open" || d.Status == "NeedSupplement") && d.ArbitratorID.HasValue)
            .GroupBy(d => d.ArbitratorID!.Value)
            .Select(g => new { ArbitratorID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ArbitratorID, x => x.Count);

        var roleNames = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.Role != null && candidates.Contains(ur.UserID))
            .Select(ur => new { ur.UserID, RoleName = ur.Role!.RoleName })
            .Distinct()
            .ToListAsync();

        return filteredCandidates
            .Select(id => new
            {
                UserID = id,
                Priority =
                    roleNames.Any(r => r.UserID == id && r.RoleName == "Moderator") ? 0 :
                    roleNames.Any(r => r.UserID == id && r.RoleName == "Manager") ? 1 : 2,
                Load = openCounts.TryGetValue(id, out var count) ? count : 0
            })
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Load)
            .ThenBy(x => x.UserID)
            .Select(x => x.UserID)
            .First();
    }

    private bool HasAnyPermission(params string[] permissions)
    {
        var userPermissions = User.Claims
            .Where(c => c.Type == "Permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return permissions.Any(userPermissions.Contains);
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

    private async Task CreateNotificationAsync(int userId, string title, string content, int transactionId)
    {
        await _notificationService.CreateAsync(new CreateNotificationOptions
        {
            UserID = userId,
            Type = "Dispute",
            Title = title,
            Content = content,
            TargetType = "Transaction",
            TargetID = transactionId,
            TransactionID = transactionId,
            Link = $"/products",
            EventKey = $"dispute:{transactionId}:{userId}:{title}"
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

    private static DisputeTicketResponse MapDispute(DisputeTicket dispute)
    {
        var result = dispute.ArbitrationResults
            .OrderByDescending(r => r.CreateTime)
            .FirstOrDefault();

        return new DisputeTicketResponse
        {
            TicketID = dispute.TicketID,
            Reason = dispute.Reason ?? "",
            Status = dispute.Status ?? "",
            OrderStatus = dispute.Transaction?.TransactionStatus ?? "",
            CreateTime = dispute.CreateTime,
            AssignTime = dispute.AssignTime,
            TransactionID = dispute.TransactionID,
            TransactionAmount = dispute.Transaction?.TransactionAmount ?? 0,
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
            RefundAmount = result?.RefundAmount,
            ResolvedTime = result?.CreateTime
        };
    }
}
