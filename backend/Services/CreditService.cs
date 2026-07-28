using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public interface ICreditService
{
    public const int MinCredit = 0;
    public const int MaxCredit = 1000;

    Task AddCreditAsync(int userId, int credit, string reason);
    Task<CreditAdjustment?> AdjustCreditAsync(int userId, int changePoints, string reason, int? operatorId = null);
    Task<bool> CanPerformAsync(int userId, string operation);
    Task<int> GetUserLevelAsync(int userId);
    int GetLevelUpRequirement(int currentLevel);
    Task<int> GetUserTotalCreditAsync(int userId);
}

public class CreditService : ICreditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreditService> _logger;

    private static readonly Dictionary<int, int> LevelThresholds = new()
    {
        { 1, 0 },
        { 2, 100 },
        { 3, 250 },
        { 4, 450 },
        { 5, 700 },
        { 6, 1000 },
        { 7, 1350 },
        { 8, 1750 },
        { 9, 2200 },
        { 10, 2700 }
    };

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
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("用户不存在: {UserId}", userId);
            return null;
        }

        var normalizedReason = NormalizeReason(reason);
        var beforeCredit = user.Credit ?? 100;
        var afterCredit = Math.Clamp(
            beforeCredit + changePoints,
            ICreditService.MinCredit,
            ICreditService.MaxCredit);
        var actualChange = afterCredit - beforeCredit;

        var previousLevel = user.UserLevel;
        user.Credit = afterCredit;
        if (actualChange > 0)
        {
            user.TotalCredit += actualChange;
        }

        var newLevel = CalculateLevelFromCredit(user.TotalCredit);
        if (newLevel > previousLevel)
        {
            _logger.LogInformation(
                "用户 {UserId} 升级到 Lv.{Level}，原因：{Reason}",
                userId,
                newLevel,
                normalizedReason);
        }

        var adjustment = new CreditAdjustment
        {
            UserID = userId,
            Description = normalizedReason,
            ChangePoints = actualChange,
            BeforeCredit = beforeCredit,
            AfterCredit = afterCredit,
            OperatorID = operatorId,
            AdjustTime = DateTime.Now
        };

        _db.CreditAdjustments.Add(adjustment);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "用户 {UserId} 信用变更 {ChangePoints}，原因：{Reason}，变更前：{BeforeCredit}，变更后：{AfterCredit}，操作人：{OperatorId}，总积分：{TotalCredit}",
            userId,
            actualChange,
            normalizedReason,
            beforeCredit,
            afterCredit,
            operatorId,
            user.TotalCredit);

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

    public async Task<int> GetUserLevelAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user?.UserLevel ?? 1;
    }

    public int GetLevelUpRequirement(int currentLevel)
    {
        if (currentLevel >= 10)
            return 2700;

        if (LevelThresholds.TryGetValue(currentLevel + 1, out var nextLevelThreshold))
        {
            var currentLevelThreshold = LevelThresholds[currentLevel];
            return nextLevelThreshold - currentLevelThreshold;
        }

        return 0;
    }

    public async Task<int> GetUserTotalCreditAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user?.TotalCredit ?? 0;
    }

    private int CalculateLevelFromCredit(int totalCredit)
    {
        for (var level = 10; level >= 1; level--)
        {
            if (LevelThresholds.TryGetValue(level, out var threshold) && totalCredit >= threshold)
            {
                return level;
            }
        }

        return 1;
    }

    private static string NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return "信用分调整";

        return normalized.Length <= 500 ? normalized : normalized[..500];
    }
}
