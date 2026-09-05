using System.Security.Claims;
using Backend.Authorization;
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
public class ForumsController : ControllerBase
{
    private const long MaxAvatarBytes = 2 * 1024 * 1024;
    private const string ManagerRoleModerator = "Moderator"; // 版主
    private const string ManagerRoleAdmin = "Admin"; // 管理员

    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IMediaStorageService _mediaStorageService;

    public ForumsController(AppDbContext db, INotificationService notificationService, IMediaStorageService mediaStorageService)
    {
        _db = db;
        _notificationService = notificationService;
        _mediaStorageService = mediaStorageService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ForumSummaryResponse>>> GetAll()
    {
        var currentUserId = TryGetCurrentUserId();
        var canSeeAll = User.IsInRole("Manager");

        var query = _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .Include(f => f.Creator)
            .Include(f => f.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .AsQueryable();

        // 普通用户仅查看开放版块；已指派的管理人员和版块创建者可以额外看到自己负责的未开放版块。
        if (!canSeeAll)
        {
            if (currentUserId.HasValue)
            {
                var userId = currentUserId.Value;
                query = query.Where(f => f.Status == "Active" ||
                    f.CreatorID == userId ||
                    f.ForumManagers.Any(fm => fm.UserID == userId));
            }
            else
            {
                query = query.Where(f => f.Status == "Active");
            }
        }

        var forums = await query
            .OrderBy(f => f.ForumName)
            .ToListAsync();

        return Ok(await MapForumsAsync(forums));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ForumSummaryResponse>> GetById(int id)
    {
        var forum = await _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .Include(f => f.Creator)
            .Include(f => f.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .FirstOrDefaultAsync(f => f.ForumID == id);

        if (forum is null || !await CanViewForumAsync(forum))
            return NotFound();

        return Ok((await MapForumsAsync(new[] { forum })).Single());
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<List<ForumSummaryResponse>>> GetMine()
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var query = _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .Include(f => f.Creator)
            .Include(f => f.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .AsQueryable();

        // 我管理的版块：仅返回创建者或被指派管理人员负责的版块。
        // 站点管理员也不例外：否则从未指派/已被移除的版块也会出现在“我管理的版块”中；
        // 管理全部版块请使用系统管理面板。
        query = query.Where(f => f.CreatorID == userId.Value ||
            f.ForumManagers.Any(fm => fm.UserID == userId.Value));

        var forums = await query.OrderBy(f => f.ForumName).ToListAsync();
        return Ok(await MapForumsAsync(forums));
    }

    /// <summary>
    /// 我关注的版块（按关注时间倒序）
    /// </summary>
    [HttpGet("joined")]
    [Authorize]
    public async Task<ActionResult<List<ForumSummaryResponse>>> GetJoined()
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var joinedForumIds = await _db.ForumMembers
            .Where(fm => fm.UserID == userId.Value)
            .OrderByDescending(fm => fm.JoinTime ?? DateTime.MinValue)
            .Select(fm => fm.ForumID)
            .ToListAsync();

        if (joinedForumIds.Count == 0)
            return Ok(new List<ForumSummaryResponse>());

        var forums = await _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .Include(f => f.Creator)
            .Include(f => f.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .Where(f => joinedForumIds.Contains(f.ForumID))
            .ToListAsync();

        // 按关注时间倒序返回
        var orderMap = joinedForumIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);
        var ordered = forums
            .OrderBy(f => orderMap.TryGetValue(f.ForumID, out var index) ? index : int.MaxValue)
            .ToList();

        return Ok(await MapForumsAsync(ordered));
    }

    [HttpPost]
    [RequirePermission("forums.create")]
    public async Task<ActionResult<ForumSummaryResponse>> Create([FromBody] CreateForumRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var name = request.ForumName.Trim();
        if (name.Length < 2)
            return BadRequest(new { message = "版块名称长度必须在2-100个字符之间" });

        var normalizedName = name.ToLower();
        if ((await _db.Forums.CountAsync(f =>
                f.ForumName != null && f.ForumName.ToLower() == normalizedName)) > 0)
            return BadRequest(new { message = "已存在同名版块" });

        var userId = TryGetCurrentUserId() ?? 0;
        var forum = new Forum
        {
            ForumName = name,
            Description = request.Description?.Trim(),
            CreatorID = userId,
            CreateTime = DateTime.Now,
            Status = "Active"
        };

        _db.Forums.Add(forum);
        await _db.SaveChangesAsync();

        // 版块必须有版主：创建者成为 Moderator 由 TRG_Forum_CreatorManager 写入，
        // 下面的重查带 Include(ForumManagers) 会直接取到触发器插入的行

        var created = await _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .Include(f => f.Creator)
            .Include(f => f.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .SingleAsync(f => f.ForumID == forum.ForumID);

        return CreatedAtAction(nameof(GetById), new { id = forum.ForumID },
            (await MapForumsAsync(new[] { created })).Single());
    }

    /// <summary>
    /// 上传版块头像（版块管理员或 Admin）
    /// </summary>
    [HttpPost("{id}/avatar")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAvatarBytes)]
    public async Task<ActionResult> UploadAvatar(int id, IFormFile? file)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum is null)
            return NotFound();

        if (!await CanManageForumAsync(id))
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请选择头像文件" });

        if (file.Length > MaxAvatarBytes)
            return BadRequest(new { message = "头像文件不能超过2MB" });

        var userId = TryGetCurrentUserId() ?? 0;

        StoredImage storedImage;
        try
        {
            storedImage = await _mediaStorageService.StoreImageAsync(file, "avatars", userId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        await SyncForumAvatarMediaAsync(id, storedImage, userId);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "版块头像已上传",
            avatarUrl = storedImage.Url
        });
    }

    /// <summary>
    /// 清除版块头像（版块管理员或 Admin）
    /// </summary>
    [HttpDelete("{id}/avatar")]
    [Authorize]
    public async Task<ActionResult> DeleteAvatar(int id)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum is null)
            return NotFound();

        if (!await CanManageForumAsync(id))
            return Forbid();

        await RemoveForumAvatarAsync(id);
        await _db.SaveChangesAsync();

        return Ok(new { message = "版块头像已移除", avatarUrl = "" });
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<ForumSummaryResponse>> Update(int id, [FromBody] UpdateForumRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var forum = await _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .Include(f => f.Creator)
            .Include(f => f.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .FirstOrDefaultAsync(f => f.ForumID == id);
        if (forum is null)
            return NotFound();

        if (!await CanManageForumAsync(id))
            return Forbid();

        var name = request.ForumName.Trim();
        if (name.Length < 2)
            return BadRequest(new { message = "版块名称长度必须在2-100个字符之间" });

        var normalizedName = name.ToLower();
        var duplicate = (await _db.Forums.CountAsync(f =>
            f.ForumID != id && f.ForumName != null && f.ForumName.ToLower() == normalizedName)) > 0;
        if (duplicate)
            return BadRequest(new { message = "已存在同名版块" });

        if (request.Status is not "Active" and not "Inactive")
            return BadRequest(new { message = "版块状态不合法" });

        forum.ForumName = name;
        forum.Description = request.Description?.Trim();
        forum.Status = request.Status;

        await _db.SaveChangesAsync();
        return Ok((await MapForumsAsync(new[] { forum })).Single());
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult> Delete(int id)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum is null)
            return NotFound();

        // 不在有内容时直接物理删除，避免帖子、审核记录成为孤儿数据。
        if ((await _db.Posts.CountAsync(p => p.ForumID == id)) > 0)
            return BadRequest(new { message = "该版块仍有帖子，不能删除；请先将版块设为 Inactive 或清理帖子" });

        var managers = await _db.ForumManagers.Where(fm => fm.ForumID == id).ToListAsync();
        if (managers.Count > 0)
            _db.ForumManagers.RemoveRange(managers);

        var members = await _db.ForumMembers.Where(fm => fm.ForumID == id).ToListAsync();
        if (members.Count > 0)
            _db.ForumMembers.RemoveRange(members);

        _db.Forums.Remove(forum);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }

    [HttpPost("{id}/managers")]
    [Authorize]
    public async Task<ActionResult> AddManager(int id, [FromBody] AssignForumManagerRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var role = NormalizeManagerRole(request.Role);
        if (role is null)
            return BadRequest(new { message = "管理人员角色不合法" });

        var forumExists = (await _db.Forums.CountAsync(f => f.ForumID == id)) > 0;
        if (!forumExists)
            return NotFound(new { message = "论坛不存在" });

        // 仅站点管理员或该版块的版主可以指派管理人员（版主额外拥有增加管理员的能力）
        if (!await CanAssignManagersAsync(id))
            return Forbid();

        var userExists = (await _db.Users.CountAsync(u => u.UserID == request.UserID && u.Status == "Active")) > 0;
        if (!userExists)
            return BadRequest(new { message = "用户不存在或不可用" });

        var exists = (await _db.ForumManagers.CountAsync(fm => fm.ForumID == id && fm.UserID == request.UserID)) > 0;
        if (!exists)
        {
            _db.ForumManagers.Add(new ForumManager { ForumID = id, UserID = request.UserID, Role = role });
            await _db.SaveChangesAsync();
        }

        var roleName = role == ManagerRoleModerator ? "版主" : "管理员";
        await CreateNotificationAsync(request.UserID, $"{roleName}权限已分配", $"你已成为论坛 #{id} 的{roleName}", id, "assigned");
        await _db.SaveChangesAsync();
        return Ok(new { message = exists ? $"该用户已经是{roleName}" : $"{roleName}已指派" });
    }

    [HttpDelete("{id}/managers/{userId}")]
    [Authorize]
    public async Task<ActionResult> RemoveManager(int id, int userId)
    {
        var forum = await _db.Forums.FirstOrDefaultAsync(f => f.ForumID == id);
        if (forum is null)
            return NotFound(new { message = "论坛不存在" });

        if (!await CanAssignManagersAsync(id))
            return Forbid();

        // 版块必须有版主：创建者是默认版主，不可被移除
        if (forum.CreatorID == userId)
            return BadRequest(new { message = "版块创建者是默认版主，不能移除" });

        var manager = await _db.ForumManagers.FirstOrDefaultAsync(fm => fm.ForumID == id && fm.UserID == userId);
        if (manager is null)
            return NotFound();

        var roleName = manager.Role == ManagerRoleAdmin ? "管理员" : "版主";
        _db.ForumManagers.Remove(manager);
        await CreateNotificationAsync(userId, $"{roleName}权限已移除", $"你不再是论坛 #{id} 的{roleName}", id, "removed");
        await _db.SaveChangesAsync();
        return Ok(new { message = $"{roleName}已移除" });
    }

    [HttpPost("{id}/join")]
    [Authorize]
    public async Task<ActionResult> Join(int id)
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var forum = await _db.Forums.FirstOrDefaultAsync(f => f.ForumID == id);
        if (forum is null)
            return NotFound();

        if (forum.Status != "Active" && !await CanManageForumAsync(id))
            return BadRequest(new { message = "该版块已停用，无法关注" });

        var exists = await _db.ForumMembers
            .CountAsync(fm => fm.ForumID == id && fm.UserID == userId.Value) > 0;
        if (!exists)
        {
            _db.ForumMembers.Add(new ForumMember
            {
                ForumID = id,
                UserID = userId.Value,
                JoinTime = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = exists ? "你已关注该版块" : "关注版块成功", joined = true });
    }

    [HttpDelete("{id}/join")]
    [Authorize]
    public async Task<ActionResult> Leave(int id)
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var member = await _db.ForumMembers
            .FirstOrDefaultAsync(fm => fm.ForumID == id && fm.UserID == userId.Value);
        if (member is not null)
        {
            _db.ForumMembers.Remove(member);
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = member is null ? "你尚未关注该版块" : "已取消关注", joined = false });
    }

    private async Task<bool> CanViewForumAsync(Forum forum)
    {
        if (User.IsInRole("Manager") || forum.Status == "Active")
            return true;

        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return false;

        return forum.CreatorID == userId.Value ||
            (await _db.ForumManagers.CountAsync(fm =>
                fm.ForumID == forum.ForumID && fm.UserID == userId.Value)) > 0;
    }

    private async Task<bool> CanManageForumAsync(int forumId)
    {
        if (User.IsInRole("Manager"))
            return true;

        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return false;

        // 创建者是默认版主，同样拥有版块管理权限
        return (await _db.Forums.CountAsync(f => f.ForumID == forumId && f.CreatorID == userId.Value)) > 0 ||
            (await _db.ForumManagers.CountAsync(fm =>
                fm.ForumID == forumId && fm.UserID == userId.Value)) > 0;
    }

    /// <summary>
    /// 指派/移除管理人员的权限：站点管理员（Manager 角色）、版块创建者或该版块的版主。
    /// 版块管理员（ForumManager 中的 Admin 角色）只能管理帖子，不能指派管理人员。
    /// </summary>
    private async Task<bool> CanAssignManagersAsync(int forumId)
    {
        if (User.IsInRole("Manager"))
            return true;

        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return false;

        return (await _db.Forums.CountAsync(f => f.ForumID == forumId && f.CreatorID == userId.Value)) > 0 ||
            (await _db.ForumManagers.CountAsync(fm =>
                fm.ForumID == forumId && fm.UserID == userId.Value && fm.Role == ManagerRoleModerator)) > 0;
    }

    private static string? NormalizeManagerRole(string? role)
    {
        return (role ?? "").Trim().ToLowerInvariant() switch
        {
            "" or "moderator" => ManagerRoleModerator,
            "admin" => ManagerRoleAdmin,
            _ => null
        };
    }

    private async Task<List<ForumSummaryResponse>> MapForumsAsync(IEnumerable<Forum> forums)
    {
        var forumList = forums.ToList();
        var currentUserId = TryGetCurrentUserId();
        var isAdmin = User.IsInRole("Manager");
        var ids = forumList.Select(f => f.ForumID).ToList();
        var publicStatuses = new[] { "Active", "Elite", "Pinned" };
        var postCounts = await _db.Posts
            .Where(p => p.ForumID.HasValue
                && ids.Contains(p.ForumID.Value)
                && publicStatuses.Contains(p.Status ?? ""))
            .GroupBy(p => p.ForumID!.Value)
            .Select(g => new { ForumID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ForumID, x => x.Count);

        var memberCounts = await _db.ForumMembers
            .Where(fm => ids.Contains(fm.ForumID))
            .GroupBy(fm => fm.ForumID)
            .Select(g => new { ForumID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ForumID, x => x.Count);

        var joinedForumIds = currentUserId.HasValue
            ? await _db.ForumMembers
                .Where(fm => fm.UserID == currentUserId.Value && ids.Contains(fm.ForumID))
                .Select(fm => fm.ForumID)
                .ToListAsync()
            : new List<int>();

        return forumList.Select(forum => new ForumSummaryResponse
        {
            ForumID = forum.ForumID,
            ForumName = forum.ForumName ?? "",
            Description = forum.Description ?? "",
            Status = forum.Status ?? "",
            AvatarUrl = forum.AvatarUrl ?? "",
            CreateTime = forum.CreateTime,
            PostCount = postCounts.TryGetValue(forum.ForumID, out var count) ? count : 0,
            CanManage = isAdmin || (currentUserId.HasValue &&
                (forum.CreatorID == currentUserId.Value ||
                 forum.ForumManagers.Any(fm => fm.UserID == currentUserId.Value))),
            CanAssignManagers = isAdmin || (currentUserId.HasValue &&
                (forum.CreatorID == currentUserId.Value ||
                 forum.ForumManagers.Any(fm => fm.UserID == currentUserId.Value && fm.Role == ManagerRoleModerator))),
            MemberCount = memberCounts.TryGetValue(forum.ForumID, out var memberCount) ? memberCount : 0,
            IsJoined = joinedForumIds.Contains(forum.ForumID),
            Creator = forum.Creator is null ? null : new ForumCreatorResponse
            {
                UserID = forum.Creator.UserID,
                Username = forum.Creator.Username ?? "",
                Email = forum.Creator.Email ?? ""
            },
            Managers = forum.ForumManagers
                .Where(fm => fm.User is not null)
                .OrderBy(fm => fm.Role == ManagerRoleAdmin ? 1 : 0)
                .Select(fm => new ForumManagerResponse
                {
                    UserID = fm.UserID,
                    Username = fm.User!.Username ?? "",
                    Email = fm.User.Email ?? "",
                    Role = fm.Role == ManagerRoleAdmin ? ManagerRoleAdmin : ManagerRoleModerator
                })
                .ToList()
        }).ToList();
    }

    private async Task SyncForumAvatarMediaAsync(int forumId, StoredImage storedImage, int uploaderId)
    {
        var existingAvatar = await _db.ForumAvatars
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.ForumID == forumId &&
                x.Media != null && x.Media.ObjectKey == storedImage.ObjectKey);

        if (existingAvatar != null)
            return;

        await RemoveForumAvatarAsync(forumId);
        // 旧头像先删除并落库，再插入新记录：同一次 SaveChanges 混合删除与插入时，
        // Oracle EF Core 生成的批次会先删 MediaFile、后删 ForumAvatar，触发 ORA-02292 外键冲突。
        await _db.SaveChangesAsync();

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
            UploadedByUserID = uploaderId
        };
        _db.MediaFiles.Add(media);
        _db.ForumAvatars.Add(new ForumAvatar { ForumID = forumId, Media = media });
    }

    private async Task RemoveForumAvatarAsync(int forumId)
    {
        var existing = await _db.ForumAvatars.Include(x => x.Media)
            .SingleOrDefaultAsync(x => x.ForumID == forumId);
        if (existing == null)
            return;

        if (existing.Media != null)
        {
            try
            {
                await _mediaStorageService.DeleteByUrlAsync(existing.Media.Url);
            }
            catch (InvalidOperationException)
            {
                // 存储侧文件已不存在时仅清理数据库记录
            }
        }

        _db.ForumAvatars.Remove(existing);
        if (existing.Media != null)
            _db.MediaFiles.Remove(existing.Media);
    }

    private async Task CreateNotificationAsync(int userId, string title, string content, int forumId, string action)
    {
        await _notificationService.CreateAsync(new CreateNotificationOptions
        {
            UserID = userId,
            Type = "Forum",
            Title = title,
            Content = content,
            TargetType = "Forum",
            TargetID = forumId,
            Link = $"/forums",
            EventKey = $"forum:manager:{forumId}:{userId}:{action}"
        });
    }

    private int? TryGetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var userId) ? userId : null;
    }
}
