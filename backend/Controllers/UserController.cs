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

    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly IMediaStorageService _mediaStorageService;

    public UserController(
        AppDbContext db,
        ICreditService creditService,
        IMediaStorageService mediaStorageService)
    {
        _db = db;
        _creditService = creditService;
        _mediaStorageService = mediaStorageService;
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
            .Include(u => u.AvatarMedia)
            .ThenInclude(a => a!.Media)
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
    /// 获取指定用户的公开资料（用于个人主页展示）
    /// </summary>
    [Authorize]
    [HttpGet("{userId}/public-profile")]
    public async Task<ActionResult> GetPublicProfile(int userId)
    {
        var user = await _db.Users
            .Include(u => u.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .FirstOrDefaultAsync(u => u.UserID == userId);

        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var currentUserId = GetCurrentUserId();
        var publicPostStatuses = new[] { "Active", "Elite", "Pinned" };
        var postCount = await _db.Posts
            .CountAsync(p => p.UserID == userId && publicPostStatuses.Contains(p.Status));
        var productCount = await _db.Products
            .CountAsync(p => p.UserID == userId && p.Status != "Inactive");
        var friendCount = await _db.FriendShips
            .CountAsync(f => (f.UserID == userId || f.FriendID == userId) && f.Status == "Accepted");

        return Ok(new
        {
            userId = user.UserID,
            username = user.Username,
            avatarUrl = user.AvatarUrl,
            bio = user.Bio,
            userLevel = user.UserLevel,
            totalCredit = user.TotalCredit,
            postCount,
            productCount,
            friendCount,
            isSelf = user.UserID == currentUserId
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

        var user = await _db.Users
            .Include(u => u.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .SingleOrDefaultAsync(u => u.UserID == currentUserId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        if (request.Username != null)
        {
            var username = request.Username.Trim();
            if (username.Length < 2 || username.Length > 50)
                return BadRequest(new { message = "用户名长度必须在2-50个字符之间" });

            if (username.Any(char.IsWhiteSpace))
                return BadRequest(new { message = "用户名不能包含空格" });

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

        if (request.AvatarUrl != null)
        {
            var avatarPath = NormalizeAvatarPath(request.AvatarUrl);
            if (avatarPath != null)
                await SyncUserAvatarUrlAsync(currentUserId, avatarPath);
            else if (string.IsNullOrWhiteSpace(request.AvatarUrl))
                await RemoveUserAvatarAsync(currentUserId);
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
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAvatarBytes)]
    public async Task<ActionResult> UploadAvatar(IFormFile? file)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0)
            return Unauthorized(new { message = "无法获取用户信息" });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请选择头像文件" });

        if (file.Length > MaxAvatarBytes)
            return BadRequest(new { message = "头像文件不能超过2MB" });

        var user = await _db.Users
            .Include(u => u.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .SingleOrDefaultAsync(u => u.UserID == currentUserId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        StoredImage storedImage;
        try
        {
            storedImage = await _mediaStorageService.StoreImageAsync(file, "avatars", currentUserId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        await SyncUserAvatarMediaAsync(currentUserId, storedImage);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "头像已上传",
            avatarUrl = storedImage.Url
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

    private static bool IsInternalAvatarUrl(string? value)
    {
        return value != null &&
            value.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task DeleteOldLocalAvatar(string? avatarUrl)
    {
        if (!IsInternalAvatarUrl(avatarUrl))
        {
            return;
        }

        await _mediaStorageService.DeleteByUrlAsync(avatarUrl);
    }

    private async Task SyncUserAvatarMediaAsync(int userId, StoredImage storedImage)
    {
        var existingAvatar = await _db.UserAvatars
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.UserID == userId &&
                x.Media != null && x.Media.ObjectKey == storedImage.ObjectKey);

        if (existingAvatar != null)
            return;

        var oldLink = await _db.UserAvatars.Include(x => x.Media)
            .SingleOrDefaultAsync(x => x.UserID == userId);
        if (oldLink != null)
        {
            await _mediaStorageService.DeleteByUrlAsync(oldLink.Media?.Url);
            _db.UserAvatars.Remove(oldLink);
            if (oldLink.Media != null)
                _db.MediaFiles.Remove(oldLink.Media);

            // Oracle EF Core 可能在同一批次中先删除 MediaFile，再删除 UserAvatar，
            // 触发 FK_UserAvatar_Media。先提交旧关联的删除，再插入新头像记录。
            await _db.SaveChangesAsync();
        }

        var media = new MediaFile
        {
            StorageProvider = storedImage.StorageProvider,
            ObjectKey = storedImage.ObjectKey,
            FileName = storedImage.FileName,
            OriginalFileName = storedImage.OriginalFileName,
            Url = storedImage.Url,
            MimeType = storedImage.MimeType,
            SizeBytes = storedImage.SizeBytes,
            ContentHash = storedImage.ContentHash,
            UploadedByUserID = userId
        };
        _db.MediaFiles.Add(media);
        _db.UserAvatars.Add(new UserAvatar { UserID = userId, Media = media });
    }

    private async Task SyncUserAvatarUrlAsync(int userId, string avatarUrl)
    {
        var existing = await _db.UserAvatars.Include(x => x.Media)
            .SingleOrDefaultAsync(x => x.UserID == userId);
        if (string.Equals(existing?.Media?.Url, avatarUrl, StringComparison.OrdinalIgnoreCase))
            return;

        await RemoveUserAvatarAsync(userId);
        var media = new MediaFile
        {
            StorageProvider = "s3",
            Url = avatarUrl,
            UploadedByUserID = userId,
            UploadTime = DateTime.UtcNow
        };
        _db.MediaFiles.Add(media);
        _db.UserAvatars.Add(new UserAvatar { UserID = userId, Media = media });
    }

    private async Task RemoveUserAvatarAsync(int userId)
    {
        var existing = await _db.UserAvatars.Include(x => x.Media)
            .SingleOrDefaultAsync(x => x.UserID == userId);
        if (existing == null)
            return;

        await DeleteOldLocalAvatar(existing.Media?.Url);
        _db.UserAvatars.Remove(existing);
        if (existing.Media != null)
            _db.MediaFiles.Remove(existing.Media);
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
