using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly ILogger<UserController> _logger;

    public UserController(AppDbContext db, ICreditService creditService, ILogger<UserController> logger)
    {
        _db = db;
        _creditService = creditService;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户等级和积分信息
    /// </summary>
    [HttpGet("{userId}/level")]
    public async Task<ActionResult<UserLevelResponse>> GetUserLevel(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var nextLevelReq = _creditService.GetLevelUpRequirement(user.UserLevel);
        
        // 计算距下一个等级还需要多少积分
        int creditToNext = 0;
        if (user.UserLevel < 10)
        {
            // 获取下一个等级的阈值
            var levelThresholds = new Dictionary<int, int>
            {
                { 2, 100 }, { 3, 250 }, { 4, 450 }, { 5, 700 },
                { 6, 1000 }, { 7, 1350 }, { 8, 1750 }, { 9, 2200 }, { 10, 2700 }
            };
            
            if (levelThresholds.TryGetValue(user.UserLevel + 1, out var nextThreshold))
            {
                creditToNext = Math.Max(0, nextThreshold - user.TotalCredit);
            }
        }

        return Ok(new UserLevelResponse
        {
            UserId = user.UserID,
            CurrentLevel = user.UserLevel,
            TotalCredit = user.TotalCredit,
            NextLevelRequirement = nextLevelReq,
            CreditToNextLevel = creditToNext
        });
    }

    /// <summary>
    /// 获取用户积分详情
    /// </summary>
    [HttpGet("{userId}/credit")]
    public async Task<ActionResult<UserCreditResponse>> GetUserCredit(int userId)
    {
        var user = await _db.Users
            .Where(u => u.UserID == userId)
            .Select(u => new UserCreditResponse
            {
                UserId = u.UserID,
                Username = u.Username,
                Email = u.Email,
                UserLevel = u.UserLevel,
                TotalCredit = u.TotalCredit,
                Credit = u.Credit ?? 0
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new { message = "用户不存在" });

        return Ok(user);
    }

    /// <summary>
    /// 添加用户积分（需要管理员权限）
    /// </summary>
    [Authorize]
    [HttpPost("credit/add")]
    public async Task<ActionResult> AddCredit([FromBody] AddCreditRequest request)
    {
        var user = await _db.Users.FindAsync(request.UserId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        await _creditService.AddCreditAsync(request.UserId, request.Credit, request.Reason);

        _logger.LogInformation("用户 {UserId} 添加积分: {Credit}，原因: {Reason}", 
            request.UserId, request.Credit, request.Reason);

        return Ok(new { message = "积分添加成功" });
    }

    /// <summary>
    /// 获取当前登录用户的信息
    /// </summary>
    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult> GetProfile()
    {
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email")?.Value;
        if (string.IsNullOrEmpty(emailClaim))
            return Unauthorized(new { message = "无法获取用户信息" });

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == emailClaim);

        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var roles = user.UserRoles.Select(ur => new
        {
            roleId = ur.RoleID,
            roleName = ur.Role?.RoleName,
            description = ur.Role?.Description
        }).ToList();

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role?.RolePermissions ?? Enumerable.Empty<RolePermission>())
            .Select(rp => new
            {
                permissionId = rp.PermissionID,
                permissionName = rp.Permission?.PermissionName,
                description = rp.Permission?.Description
            })
            .Distinct()
            .ToList();

        return Ok(new
        {
            userId = user.UserID,
            username = user.Username,
            email = user.Email,
            userLevel = user.UserLevel,
            totalCredit = user.TotalCredit,
            credit = user.Credit,
            status = user.Status,
            roles = roles,
            permissions = permissions
        });
    }
}
