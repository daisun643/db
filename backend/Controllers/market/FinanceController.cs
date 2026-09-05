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

    // 与视图 "V_FinanceFlow" 的 flowType 取值保持一致
    private const string IncomeType = "收入";
    private const string ExpenseType = "支出";

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

        // 收入与支出的口径由视图 "V_FinanceFlow" 固化，应用层只做过滤、汇总与排序
        var rows = await _db.FinanceFlows
            .Where(f => f.UserID == userId)
            .ToListAsync();

        var flows = rows
            .Select(f => new FlowItem
            {
                Type = f.FlowType ?? "",
                Amount = f.Amount,
                Status = f.Status ?? "",
                Time = f.FlowTime,
                TransactionId = f.TransactionID,
                Description = f.Description ?? ""
            })
            .OrderByDescending(f => f.Time)
            .ToList();

        return Ok(new
        {
            TotalCount = flows.Count,
            TotalIncome = flows.Where(f => f.Type == IncomeType).Sum(f => f.Amount),
            TotalExpense = flows.Where(f => f.Type == ExpenseType).Sum(f => f.Amount),
            Flows = flows
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

        var rows = await _db.FinanceFlows
            .Where(f => f.UserID == userId)
            .ToListAsync();

        // 总收入与流水口径一致：出售所得 + 钱包充值 + 纠纷退款/结算
        var totalIncome = rows.Where(f => f.FlowType == IncomeType).Sum(f => f.Amount);
        var totalExpense = rows.Where(f => f.FlowType == ExpenseType).Sum(f => f.Amount);
        // 冻结金额：已支付但尚未确认收货的订单
        var frozenAmount = rows
            .Where(f => f.FlowType == ExpenseType && f.Status == "Paid")
            .Sum(f => f.Amount);

        return Ok(new
        {
            Balance = availableBalance,
            FrozenAmount = frozenAmount,
            AvailableAmount = availableBalance - frozenAmount,
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
