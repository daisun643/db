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
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;

    public ForumsController(AppDbContext db, INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ForumSummaryResponse>>> GetAll()
    {
        var currentUserId = TryGetCurrentUserId();
        var canSeeAll = User.IsInRole("Admin");

        var query = _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .AsQueryable();

        // 普通用户仅查看开放版块；已指派的版主可以额外看到自己负责的未开放版块。
        if (!canSeeAll)
        {
            if (currentUserId.HasValue)
            {
                var userId = currentUserId.Value;
                query = query.Where(f => f.Status == "Active" ||
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
            .AsQueryable();

        query = User.IsInRole("Admin")
            ? query
            : query.Where(f => f.ForumManagers.Any(fm => fm.UserID == userId.Value));

        var forums = await query.OrderBy(f => f.ForumName).ToListAsync();
        return Ok(await MapForumsAsync(forums));
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

        return CreatedAtAction(nameof(GetById), new { id = forum.ForumID },
            (await MapForumsAsync(new[] { forum })).Single());
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum is null)
            return NotFound();

        // 不在有内容时直接物理删除，避免帖子、标签、审核记录成为孤儿数据。
        if ((await _db.Posts.CountAsync(p => p.ForumID == id)) > 0)
            return BadRequest(new { message = "该版块仍有帖子，不能删除；请先将版块设为 Inactive 或清理帖子" });

        var managers = await _db.ForumManagers.Where(fm => fm.ForumID == id).ToListAsync();
        if (managers.Count > 0)
            _db.ForumManagers.RemoveRange(managers);

        _db.Forums.Remove(forum);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }

    [HttpPost("{id}/managers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AddManager(int id, [FromBody] AssignForumManagerRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var forumExists = (await _db.Forums.CountAsync(f => f.ForumID == id)) > 0;
        if (!forumExists)
            return NotFound(new { message = "论坛不存在" });

        var userExists = (await _db.Users.CountAsync(u => u.UserID == request.UserID && u.Status == "Active")) > 0;
        if (!userExists)
            return BadRequest(new { message = "用户不存在或不可用" });

        var exists = (await _db.ForumManagers.CountAsync(fm => fm.ForumID == id && fm.UserID == request.UserID)) > 0;
        if (!exists)
        {
            _db.ForumManagers.Add(new ForumManager { ForumID = id, UserID = request.UserID });
            _db.Notifications.Add(new Notification
            {
                UserID = request.UserID,
                Title = "版主权限已分配",
                Content = $"你已成为论坛 #{id} 的版主",
                CreateTime = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        await CreateNotificationAsync(request.UserID, "版主权限已分配", $"你已成为论坛 #{id} 的版主", id, "assigned");
        await _db.SaveChangesAsync();
        return Ok(new { message = exists ? "该用户已经是版主" : "版主已指派" });
    }

    [HttpDelete("{id}/managers/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RemoveManager(int id, int userId)
    {
        var manager = await _db.ForumManagers.FirstOrDefaultAsync(fm => fm.ForumID == id && fm.UserID == userId);
        if (manager is null)
            return NotFound();

        _db.ForumManagers.Remove(manager);
        await CreateNotificationAsync(userId, "版主权限已移除", $"你不再是论坛 #{id} 的版主", id, "removed");
        await _db.SaveChangesAsync();
        return Ok(new { message = "版主已移除" });
    }

    private async Task<bool> CanViewForumAsync(Forum forum)
    {
        if (User.IsInRole("Admin") || forum.Status == "Active")
            return true;

        var userId = TryGetCurrentUserId();
        return userId.HasValue && (await _db.ForumManagers.CountAsync(fm =>
            fm.ForumID == forum.ForumID && fm.UserID == userId.Value)) > 0;
    }

    private async Task<bool> CanManageForumAsync(int forumId)
    {
        if (User.IsInRole("Admin"))
            return true;

        var userId = TryGetCurrentUserId();
        return userId.HasValue && await _db.ForumManagers.CountAsync(fm =>
            fm.ForumID == forumId && fm.UserID == userId.Value) > 0;
    }

    private async Task<List<ForumSummaryResponse>> MapForumsAsync(IEnumerable<Forum> forums)
    {
        var forumList = forums.ToList();
        var currentUserId = TryGetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var ids = forumList.Select(f => f.ForumID).ToList();
        var publicStatuses = new[] { "Active", "Elite", "Pinned" };
        var postCounts = await _db.Posts
            .Where(p => p.ForumID.HasValue
                && ids.Contains(p.ForumID.Value)
                && publicStatuses.Contains(p.Status ?? ""))
            .GroupBy(p => p.ForumID!.Value)
            .Select(g => new { ForumID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ForumID, x => x.Count);

        return forumList.Select(forum => new ForumSummaryResponse
        {
            ForumID = forum.ForumID,
            ForumName = forum.ForumName ?? "",
            Description = forum.Description ?? "",
            Status = forum.Status ?? "",
            CreateTime = forum.CreateTime,
            PostCount = postCounts.TryGetValue(forum.ForumID, out var count) ? count : 0,
            CanManage = isAdmin || (currentUserId.HasValue &&
                forum.ForumManagers.Any(fm => fm.UserID == currentUserId.Value)),
            Managers = forum.ForumManagers
                .Where(fm => fm.User is not null)
                .Select(fm => new ForumManagerResponse
                {
                    UserID = fm.UserID,
                    Username = fm.User!.Username ?? "",
                    Email = fm.User.Email ?? ""
                })
                .ToList()
        }).ToList();
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
