using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public interface ICreditService
{
    /// <summary>
    /// 添加积分并检查升级
    /// </summary>
    Task AddCreditAsync(int userId, int credit, string reason);
    
    /// <summary>
    /// 获取用户当前等级
    /// </summary>
    Task<int> GetUserLevelAsync(int userId);
    
    /// <summary>
    /// 获取用户升级所需的积分
    /// </summary>
    int GetLevelUpRequirement(int currentLevel);
    
    /// <summary>
    /// 获取用户当前积分
    /// </summary>
    Task<int> GetUserTotalCreditAsync(int userId);
}

public class CreditService : ICreditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreditService> _logger;
    
    // 积分阈值配置：每个等级所需的累计积分
    private static readonly Dictionary<int, int> LevelThresholds = new()
    {
        { 1, 0 },      // Lv.1: 0 积分起点
        { 2, 100 },    // Lv.2: 100 积分
        { 3, 250 },    // Lv.3: 250 积分
        { 4, 450 },    // Lv.4: 450 积分
        { 5, 700 },    // Lv.5: 700 积分
        { 6, 1000 },   // Lv.6: 1000 积分
        { 7, 1350 },   // Lv.7: 1350 积分
        { 8, 1750 },   // Lv.8: 1750 积分
        { 9, 2200 },   // Lv.9: 2200 积分
        { 10, 2700 }   // Lv.10: 2700 积分
    };

    public CreditService(AppDbContext db, ILogger<CreditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddCreditAsync(int userId, int credit, string reason)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("用户不存在: {UserId}", userId);
            return;
        }

        // 添加积分
        user.TotalCredit += credit;
        
        // 检查是否升级
        int newLevel = CalculateLevelFromCredit(user.TotalCredit);
        if (newLevel > user.UserLevel)
        {
            user.UserLevel = newLevel;
            _logger.LogInformation("用户 {UserId} 升级到 Lv.{Level}，原因：{Reason}", userId, newLevel, reason);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("用户 {UserId} 添加积分 +{Credit}，原因：{Reason}，总积分：{TotalCredit}", 
            userId, credit, reason, user.TotalCredit);
    }

    public async Task<int> GetUserLevelAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user?.UserLevel ?? 1;
    }

    public int GetLevelUpRequirement(int currentLevel)
    {
        if (currentLevel >= 10)
            return 2700; // 最高等级无需升级

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

    /// <summary>
    /// 根据总积分计算等级
    /// </summary>
    private int CalculateLevelFromCredit(int totalCredit)
    {
        for (int level = 10; level >= 1; level--)
        {
            if (LevelThresholds.TryGetValue(level, out var threshold) && totalCredit >= threshold)
            {
                return level;
            }
        }
        return 1;
    }
}
