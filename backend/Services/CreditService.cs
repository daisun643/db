using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public interface ICreditService
{
    // 上下界由存储过程 "sp_adjust_credit" 负责夹取，这里仅作为对外暴露的常量
    public const int MinCredit = 0;
    public const int MaxCredit = 1000;

    Task AddCreditAsync(int userId, int credit, string reason);
    Task<CreditAdjustment?> AdjustCreditAsync(int userId, int changePoints, string reason, int? operatorId = null);
    Task<bool> CanPerformAsync(int userId, string operation);
}

public class CreditService : ICreditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreditService> _logger;

    public CreditService(AppDbContext db, ILogger<CreditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddCreditAsync(int userId, int credit, string reason)
    {
        await AdjustCreditAsync(userId, credit, reason);
    }

    public async Task<CreditAdjustment?> AdjustCreditAsync(
        int userId,
        int changePoints,
        string reason,
        int? operatorId = null)
    {
        // 0~1000 夹取、User.credit 更新与 CreditAdjustment 审计流水都在存储过程内原子完成
        var adjustmentId = OracleProcedure.OutInt32("p_adjustment_id");
        await OracleProcedure.CallAsync(_db, "sp_adjust_credit",
            OracleProcedure.InInt32("p_user_id", userId),
            OracleProcedure.InInt32("p_change_points", changePoints),
            OracleProcedure.InText("p_reason", reason),
            OracleProcedure.InInt32("p_operator_id", operatorId),
            OracleProcedure.OutInt32("p_before_credit"),
            OracleProcedure.OutInt32("p_after_credit"),
            OracleProcedure.OutInt32("p_real_change"),
            adjustmentId);

        var id = OracleProcedure.ReadInt32(adjustmentId);
        if (id <= 0)
        {
            _logger.LogWarning("用户不存在: {UserId}", userId);
            return null;
        }

        var adjustment = await _db.CreditAdjustments
            .Include(a => a.Operator)
            .FirstOrDefaultAsync(a => a.CreditAdjustmentID == id);

        _logger.LogInformation(
            "用户 {UserId} 信用变更 {ChangePoints}，原因：{Reason}，变更前：{BeforeCredit}，变更后：{AfterCredit}，操作人：{OperatorId}",
            userId,
            adjustment?.ChangePoints ?? changePoints,
            adjustment?.Description ?? reason,
            adjustment?.BeforeCredit,
            adjustment?.AfterCredit,
            operatorId);

        return adjustment;
    }

    public async Task<bool> CanPerformAsync(int userId, string operation)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.Status != "Active")
            return false;

        var credit = user.Credit ?? 0;
        return operation switch
        {
            "post" => credit >= 60,
            "comment" => credit >= 40,
            "product.publish" => credit >= 50,
            "order.create" => credit >= 50,
            _ => credit > 0
        };
    }
}
