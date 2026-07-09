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
    private const long MaxAvatarBytes = 2 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedAvatarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp"
    };

    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly ILogger<UserController> _logger;
    private readonly IWebHostEnvironment _environment;

    public UserController(
        AppDbContext db,
        ICreditService creditService,
        ILogger<UserController> logger,
        IWebHostEnvironment environment)
    {
        _db = db;
        _creditService = creditService;
        _logger = logger;
        _environment = environment;
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
    [Authorize]
    [HttpGet("{userId}/credit")]
    public async Task<ActionResult<UserCreditResponse>> GetUserCredit(int userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0)
            return Unauthorized(new { message = "无法获取用户信息" });

        if (currentUserId != userId && !CanManageCredit())
            return Forbid();

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
            .Include(a => a.Operator)
            .Where(a => a.UserID == currentUserId)
            .OrderByDescending(a => a.AdjustTime)
            .Take(50)
            .ToListAsync();

        return Ok(adjustments.Select(MapCreditAdjustment).ToList());
    }

    [Authorize]
    [RequirePermission("users.view", "dashboard.view")]
    [HttpGet("{userId}/credit-adjustments")]
    public async Task<ActionResult<List<CreditAdjustmentResponse>>> GetUserCreditAdjustments(int userId)
    {
        var userExists = await _db.Users.CountAsync(u => u.UserID == userId) > 0;
        if (!userExists)
            return NotFound(new { message = "用户不存在" });

        var adjustments = await _db.CreditAdjustments
            .Include(a => a.Operator)
            .Where(a => a.UserID == userId)
            .OrderByDescending(a => a.AdjustTime)
            .Take(50)
            .ToListAsync();

        return Ok(adjustments.Select(MapCreditAdjustment).ToList());
    }

    /// <summary>
    /// 添加用户积分（需要管理员权限）
    /// </summary>
    [Authorize]
    [RequirePermission("users.edit", "users.ban", "dashboard.view")]
    [HttpPost("credit/add")]
    public async Task<ActionResult> AddCredit([FromBody] AddCreditRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "请求体不能为空" });

        if (request.Credit == 0)
            return BadRequest(new { message = "调整分值不能为 0" });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "调整原因不能为空" });

        var user = await _db.Users.FindAsync(request.UserId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var operatorId = GetCurrentUserId();
        var adjustment = await _creditService.AdjustCreditAsync(
            request.UserId,
            request.Credit,
            request.Reason,
            operatorId == 0 ? null : operatorId);

        if (adjustment == null)
            return NotFound(new { message = "用户不存在" });

        await _db.Entry(adjustment).Reference(a => a.Operator).LoadAsync();

        return Ok(new
        {
            message = "信用分调整成功",
            adjustment = MapCreditAdjustment(adjustment)
        });

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
            nickname = user.Nickname,
            avatarUrl = user.AvatarUrl,
            contact = user.Contact,
            bio = user.Bio,
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
        if (request == null)
            return BadRequest(new { message = "请求体不能为空" });

        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0)
            return Unauthorized(new { message = "无法获取用户信息" });

        var user = await _db.Users.FindAsync(currentUserId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        if (request.Username != null)
        {
            var username = request.Username.Trim();
            if (username.Length < 2 || username.Length > 50)
                return BadRequest(new { message = "用户名长度必须在2-50个字符之间" });

            var existingUsernameUserId = await _db.Users
                .Where(u => u.UserID != currentUserId &&
                            u.Username != null &&
                            u.Username.ToLower() == username.ToLower())
                .Select(u => u.UserID)
                .FirstOrDefaultAsync();
            if (existingUsernameUserId != 0)
                return BadRequest(new { message = "该用户名已被使用" });

            user.Username = username;
        }

        if (request.Nickname != null)
            user.Nickname = NormalizeProfileField(request.Nickname, 50);
        if (request.AvatarUrl != null)
        {
            var avatarPath = NormalizeAvatarPath(request.AvatarUrl);
            if (avatarPath != null || string.IsNullOrWhiteSpace(request.AvatarUrl))
                user.AvatarUrl = avatarPath;
        }
        if (request.Contact != null)
            user.Contact = NormalizeProfileField(request.Contact, 100);
        if (request.Bio != null)
            user.Bio = NormalizeProfileField(request.Bio, 500);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "资料已更新",
            userId = user.UserID,
            username = user.Username,
            email = user.Email,
            nickname = user.Nickname,
            avatarUrl = user.AvatarUrl,
            contact = user.Contact,
            bio = user.Bio,
            userLevel = user.UserLevel,
            totalCredit = user.TotalCredit,
            credit = user.Credit,
            status = user.Status
        });
    }

    /// <summary>
    /// 上传当前登录用户头像
    /// </summary>
    [Authorize]
    [HttpPost("avatar")]
    [RequestSizeLimit(MaxAvatarBytes)]
    public async Task<ActionResult> UploadAvatar([FromForm] IFormFile? file)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0)
            return Unauthorized(new { message = "无法获取用户信息" });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请选择头像文件" });

        if (file.Length > MaxAvatarBytes)
            return BadRequest(new { message = "头像文件不能超过2MB" });

        if (!AllowedAvatarTypes.TryGetValue(file.ContentType, out var extension))
            return BadRequest(new { message = "仅支持 JPG、PNG、GIF、WebP 图片" });

        if (!HasValidImageSignature(file, extension))
            return BadRequest(new { message = "头像文件格式不正确" });

        var user = await _db.Users.FindAsync(currentUserId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var avatarDirectory = Path.Combine(webRootPath, "uploads", "avatars");
        Directory.CreateDirectory(avatarDirectory);

        var fileName = $"{currentUserId}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(avatarDirectory, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        DeleteOldLocalAvatar(webRootPath, user.AvatarUrl);

        user.AvatarUrl = $"/uploads/avatars/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "头像已上传",
            avatarUrl = user.AvatarUrl
        });
    }

    /// <summary>
    /// 修改当前登录用户密码
    /// </summary>
    [Authorize]
    [HttpPost("password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "请求体不能为空" });

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

    private bool CanManageCredit()
    {
        if (User.IsInRole("Admin"))
            return true;

        var permissions = User.Claims
            .Where(c => c.Type == "Permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return permissions.Contains("users.view") ||
               permissions.Contains("users.edit") ||
               permissions.Contains("users.ban") ||
               permissions.Contains("dashboard.view");
    }

    private static CreditAdjustmentResponse MapCreditAdjustment(CreditAdjustment adjustment)
    {
        return new CreditAdjustmentResponse
        {
            CreditAdjustmentId = adjustment.CreditAdjustmentID,
            UserId = adjustment.UserID,
            Description = adjustment.Description ?? "",
            ChangePoints = adjustment.ChangePoints ?? 0,
            BeforeCredit = adjustment.BeforeCredit,
            AfterCredit = adjustment.AfterCredit,
            OperatorId = adjustment.OperatorID,
            OperatorName = adjustment.Operator?.Username ?? adjustment.Operator?.Email,
            AdjustTime = adjustment.AdjustTime
        };
    }

    private static string? NormalizeProfileField(string? value, int maxLength)
    {
        if (value == null) return null;

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            trimmed = trimmed[..maxLength];
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeAvatarPath(string? value)
    {
        var normalized = NormalizeProfileField(value, 500);
        if (normalized == null) return null;

        return normalized.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : null;
    }

    private static bool HasValidImageSignature(IFormFile file, string extension)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = file.OpenReadStream();
        var bytesRead = stream.Read(header);

        return extension switch
        {
            ".jpg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => bytesRead >= 8 &&
                      header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                      header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            ".gif" => bytesRead >= 6 &&
                      header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 &&
                      header[3] == 0x38 && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61,
            ".webp" => bytesRead >= 12 &&
                       header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                       header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false
        };
    }

    private static void DeleteOldLocalAvatar(string webRootPath, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl) ||
            !avatarUrl.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(avatarUrl);
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var oldPath = Path.Combine(webRootPath, "uploads", "avatars", fileName);
        if (System.IO.File.Exists(oldPath))
        {
            System.IO.File.Delete(oldPath);
        }
    }

    private static bool IsValidPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        if (password.Length < 8) return false;

        return password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit);
    }
}
