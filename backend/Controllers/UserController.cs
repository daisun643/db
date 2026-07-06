using Backend.Authorization;
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

    [Authorize]
    [HttpGet("credit-adjustments")]
    public async Task<ActionResult<List<CreditAdjustmentResponse>>> GetMyCreditAdjustments()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0)
            return Unauthorized(new { message = "无法获取用户信息" });

        var adjustments = await _db.CreditAdjustments
            .Where(a => a.UserID == currentUserId)
            .OrderByDescending(a => a.AdjustTime)
            .Take(50)
            .Select(a => new CreditAdjustmentResponse
            {
                CreditAdjustmentId = a.CreditAdjustmentID,
                UserId = a.UserID,
                Description = a.Description ?? "",
                ChangePoints = a.ChangePoints ?? 0,
                AdjustTime = a.AdjustTime
            })
            .ToListAsync();

        return Ok(adjustments);
    }

    /// <summary>
    /// 添加用户积分（需要管理员权限）
    /// </summary>
    [Authorize]
    [RequirePermission("users.edit", "users.ban", "dashboard.view")]
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
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0)
            return Unauthorized(new { message = "无法获取用户信息" });

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r!.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.UserID == currentUserId);

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

    /// <summary>
    /// 更新当前登录用户的基本资料
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0)
            return Unauthorized(new { message = "无法获取用户信息" });

        var username = request.Username.Trim();
        if (username.Length < 2 || username.Length > 50)
            return BadRequest(new { message = "用户名长度必须在2-50个字符之间" });

        var user = await _db.Users.FindAsync(currentUserId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var usernameExists = await _db.Users.AnyAsync(u =>
            u.UserID != currentUserId &&
            u.Username != null &&
            u.Username.ToLower() == username.ToLower());
        if (usernameExists)
            return BadRequest(new { message = "该用户名已被使用" });

        user.Username = username;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "资料已更新",
            userId = user.UserID,
            username = user.Username,
            email = user.Email,
            userLevel = user.UserLevel,
            totalCredit = user.TotalCredit,
            credit = user.Credit,
            status = user.Status
        });
    }

    /// <summary>
    /// 修改当前登录用户密码
    /// </summary>
    [Authorize]
    [HttpPost("password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0)
            return Unauthorized(new { message = "无法获取用户信息" });

        var user = await _db.Users.FindAsync(currentUserId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "当前密码错误" });
        }

        if (!IsValidPassword(request.NewPassword))
            return BadRequest(new { message = "新密码必须至少8位，包含大小写字母和数字" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = "密码已修改" });
    }

    private int GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userId, out var id) ? id : 0;
    }

    private static bool IsValidPassword(string password)
    {
        if (password.Length < 8) return false;

        return password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit);
    }
}
