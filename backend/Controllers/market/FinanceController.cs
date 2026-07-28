using Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Controllers.market;

[ApiController]
[Route("api/finance")]
[Authorize]
public class FinanceController : ControllerBase
{
    private readonly AppDbContext _db;

    public FinanceController(AppDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    [HttpGet("flows")]
    public async Task<IActionResult> GetFlows()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Unauthorized();

        // 内部类定义在方法外部（为了简洁，这里直接放内部）
        var outflows = new List<FlowItem>();
        var outflowsQuery = _db.Transactions
            .Include(t => t.Product)
            .Where(t => t.UserID == userId
                        && (t.TransactionStatus == "Paid"
                            || t.TransactionStatus == "Completed"
                            || t.TransactionStatus == "Disputed"
                            || t.TransactionStatus == "Refunded"));

        foreach (var t in await outflowsQuery.ToListAsync())
        {
            outflows.Add(new FlowItem
            {
                Type = "支出",
                Amount = t.TransactionAmount ?? 0,
                Status = t.TransactionStatus ?? "",
                Time = t.PayTime ?? t.CreateTime,
                TransactionId = t.TransactionID,
                Description = t.Product != null ? $"购买商品：{t.Product.Title}" : "订单消费（商品已下架）"
            });
        }

        var inflows = new List<FlowItem>();
        var inflowsQuery = _db.Transactions
            .Include(t => t.Product)
            .Where(t => t.Product != null
                        && t.Product.UserID == userId
                        && t.TransactionStatus == "Completed");

        foreach (var t in await inflowsQuery.ToListAsync())
        {
            inflows.Add(new FlowItem
            {
                Type = "收入",
                Amount = t.TransactionAmount ?? 0,
                Status = t.TransactionStatus ?? "",
                Time = t.PayTime ?? t.CreateTime,
                TransactionId = t.TransactionID,
                Description = t.Product != null ? $"出售商品：{t.Product.Title}" : "商品收入（商品已下架）"
            });
        }

        var allFlows = outflows
            .Concat(inflows)
            .OrderByDescending(f => f.Time)
            .ToList();

        return Ok(new
        {
            TotalCount = allFlows.Count,
            TotalIncome = inflows.Sum(f => f.Amount),
            TotalExpense = outflows.Sum(f => f.Amount),
            Flows = allFlows
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Unauthorized();

        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserID == userId);
        var availableBalance = wallet?.Balance ?? 0;  // 当前可用余额（扣减在支付环节）

        var frozenAmount = await _db.Transactions
            .Where(t => t.UserID == userId && t.TransactionStatus == "Paid")
            .SumAsync(t => t.TransactionAmount ?? 0);

        var balance = availableBalance;
        var availableAmount = availableBalance - frozenAmount;

        var totalIncome = await _db.Transactions
            .Include(t => t.Product)
            .Where(t => t.Product != null && t.Product.UserID == userId && t.TransactionStatus == "Completed")
            .SumAsync(t => t.TransactionAmount ?? 0);

        var totalExpense = await _db.Transactions
            .Where(t => t.UserID == userId
                        && (t.TransactionStatus == "Paid"
                            || t.TransactionStatus == "Completed"
                            || t.TransactionStatus == "Disputed"
                            || t.TransactionStatus == "Refunded"))
            .SumAsync(t => t.TransactionAmount ?? 0);

        return Ok(new
        {
            Balance = balance,
            FrozenAmount = frozenAmount,
            AvailableAmount = availableAmount,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            NetAmount = totalIncome - totalExpense
        });
    }
    // 内部类用于流水项
    private class FlowItem
    {
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? Time { get; set; }
        public int? TransactionId { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
