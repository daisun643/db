using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Authorization;
using Backend.Services;
using Backend.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmailSettings _emailSettings;

    public UsersController(AppDbContext db, IOptions<EmailSettings> emailSettings)
    {
        _db = db;
        _emailSettings = emailSettings.Value;
    }

    [HttpGet]
    [RequirePermission("users.view")]
    public async Task<ActionResult<List<AdminUserResponse>>> GetAll()
    {
        var users = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.UserID)
            .ToListAsync();

        return Ok(users.Select(MapAdminUser).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminUserResponse>> GetById(int id)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var isAdmin = User.IsInRole("Admin");
        
        if (id != currentUserId && !isAdmin)
            return Forbid();

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserID == id);
        return user is null ? NotFound() : Ok(MapAdminUser(user));
    }

    [HttpPost]
    [RequirePermission("users.create")]
    public async Task<ActionResult<AdminUserResponse>> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var email = request.Email.Trim();
        var username = request.Username.Trim();
        if (!ValidateEmailDomain(email))
            return BadRequest(new { message = $"仅支持 @{_emailSettings.AllowedDomain} 邮箱" });

        if (!ValidatePassword(request.Password))
            return BadRequest(new { message = "密码必须包含大小写字母和数字" });

        var exists = await _db.Users.AnyAsync(u => u.Email == email);
        if (exists)
            return BadRequest(new { message = "该邮箱已存在" });

        var roleIds = request.RoleIds.Distinct().ToList();
        if (roleIds.Count == 0)
        {
            var defaultRoleId = await _db.Roles
                .Where(r => r.RoleName == "User")
                .Select(r => r.RoleID)
                .FirstOrDefaultAsync();
            if (defaultRoleId > 0)
                roleIds.Add(defaultRoleId);
        }

        if (roleIds.Count > 0)
        {
            var validRoleCount = await _db.Roles.CountAsync(r => roleIds.Contains(r.RoleID));
            if (validRoleCount != roleIds.Count)
                return BadRequest(new { message = "包含不存在的角色" });
        }

        var user = new User
        {
            Email = email,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Credit = 100,
            Status = "Active",
            UserCode = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            UserLevel = 1,
            TotalCredit = 0
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        foreach (var roleId in roleIds)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserID = user.UserID,
                RoleID = roleId,
                AssignTime = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();
        user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.UserID == user.UserID);

        return CreatedAtAction(nameof(GetById), new { id = user.UserID }, MapAdminUser(user));
    }

    private bool ValidateEmailDomain(string email)
    {
        var domain = email.Split('@').LastOrDefault();
        return domain?.Equals(_emailSettings.AllowedDomain, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool ValidatePassword(string password)
    {
        return password.Length >= 8 &&
            password.Any(char.IsUpper) &&
            password.Any(char.IsLower) &&
            password.Any(char.IsDigit);
    }

    private static AdminUserResponse MapAdminUser(User user)
    {
        return new AdminUserResponse
        {
            UserID = user.UserID,
            Username = user.Username ?? "",
            Email = user.Email ?? "",
            UserCode = user.UserCode ?? "",
            Credit = user.Credit ?? 0,
            Status = user.Status ?? "",
            UserLevel = user.UserLevel,
            TotalCredit = user.TotalCredit,
            Roles = user.UserRoles
                .Select(ur => ur.Role?.RoleName)
                .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
                .Cast<string>()
                .ToList()
        };
    }
}

[ApiController]
[Route("api/[controller]")]
public class ForumsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ForumsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ForumSummaryResponse>>> GetAll()
    {
        var forums = await _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .ToListAsync();

        return Ok(forums.Select(MapForum).ToList());
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ForumSummaryResponse>> GetById(int id)
    {
        var forum = await _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .FirstOrDefaultAsync(f => f.ForumID == id);

        return forum is null ? NotFound() : Ok(MapForum(forum));
    }

    [HttpPost]
    [RequirePermission("forums.create")]
    public async Task<ActionResult<ForumSummaryResponse>> Create([FromBody] CreateForumRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var forum = new Forum
        {
            ForumName = request.ForumName.Trim(),
            Description = request.Description,
            CreatorID = userId,
            CreateTime = DateTime.Now,
            Status = "Active"
        };

        _db.Forums.Add(forum);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = forum.ForumID }, MapForum(forum));
    }

    [HttpPut("{id}")]
    [RequirePermission("forums.edit")]
    public async Task<ActionResult<ForumSummaryResponse>> Update(int id, [FromBody] UpdateForumRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var forum = await _db.Forums.FindAsync(id);
        if (forum == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var canManage = User.IsInRole("Admin") ||
            await _db.ForumManagers.AnyAsync(fm => fm.ForumID == id && fm.UserID == userId);
        if (!canManage)
            return Forbid();

        forum.ForumName = request.ForumName.Trim();
        forum.Description = request.Description;
        forum.Status = request.Status is "Active" or "Inactive" ? request.Status : "Active";

        await _db.SaveChangesAsync();
        return Ok(MapForum(forum));
    }

    [HttpDelete("{id}")]
    [RequirePermission("forums.delete")]
    public async Task<ActionResult> Delete(int id)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum == null)
            return NotFound();

        _db.Forums.Remove(forum);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }

    [HttpPost("{id}/managers")]
    [RequirePermission("forums.edit")]
    public async Task<ActionResult> AddManager(int id, [FromBody] AssignForumManagerRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var forumExists = await _db.Forums.AnyAsync(f => f.ForumID == id);
        if (!forumExists)
            return NotFound(new { message = "论坛不存在" });

        var userExists = await _db.Users.AnyAsync(u => u.UserID == request.UserID && u.Status == "Active");
        if (!userExists)
            return BadRequest(new { message = "用户不存在或不可用" });

        var exists = await _db.ForumManagers.AnyAsync(fm => fm.ForumID == id && fm.UserID == request.UserID);
        if (!exists)
        {
            _db.ForumManagers.Add(new ForumManager { ForumID = id, UserID = request.UserID });
            await _db.SaveChangesAsync();
        }

        await CreateNotificationAsync(request.UserID, "版主权限已分配", $"你已成为论坛 #{id} 的版主");
        await _db.SaveChangesAsync();
        return Ok(new { message = "版主已指派" });
    }

    [HttpDelete("{id}/managers/{userId}")]
    [RequirePermission("forums.edit")]
    public async Task<ActionResult> RemoveManager(int id, int userId)
    {
        var manager = await _db.ForumManagers.FirstOrDefaultAsync(fm => fm.ForumID == id && fm.UserID == userId);
        if (manager == null)
            return NotFound();

        _db.ForumManagers.Remove(manager);
        await CreateNotificationAsync(userId, "版主权限已移除", $"你不再是论坛 #{id} 的版主");
        await _db.SaveChangesAsync();
        return Ok(new { message = "版主已移除" });
    }

    private ForumSummaryResponse MapForum(Forum forum)
    {
        return new ForumSummaryResponse
        {
            ForumID = forum.ForumID,
            ForumName = forum.ForumName ?? "",
            Description = forum.Description ?? "",
            Status = forum.Status ?? "",
            CreateTime = forum.CreateTime,
            PostCount = _db.Posts.Count(p => p.ForumID == forum.ForumID && p.Status != "Deleted"),
            Managers = forum.ForumManagers
                .Where(fm => fm.User != null)
                .Select(fm => new ForumManagerResponse
                {
                    UserID = fm.UserID,
                    Username = fm.User!.Username ?? "",
                    Email = fm.User.Email ?? ""
                })
                .ToList()
        };
    }

    private async Task CreateNotificationAsync(int userId, string title, string content)
    {
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Title = title,
            Content = content,
            CreateTime = DateTime.Now
        });
        await Task.CompletedTask;
    }
}

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private static readonly string[] SensitiveWords = ["违禁", "敏感词", "spam"];

    public PostsController(AppDbContext db, ICreditService creditService)
    {
        _db = db;
        _creditService = creditService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<PostListItemResponse>>> GetAll(
        [FromQuery] int? forumId,
        [FromQuery] string? keyword,
        [FromQuery] string? tag,
        [FromQuery] string? status,
        [FromQuery] string? sort = "latest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Posts
            .Include(p => p.User)
            .Include(p => p.Forum)
            .AsQueryable();

        if (forumId.HasValue)
            query = query.Where(p => p.ForumID == forumId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(p =>
                (p.Title != null && p.Title.Contains(trimmed)) ||
                (p.Content != null && p.Content.Contains(trimmed)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!IsPublicPostStatus(status) && !CanViewModerationStatus())
                return Forbid();

            query = query.Where(p => p.Status == status);
        }
        else
        {
            query = query.Where(p => p.Status == "Active" || p.Status == "Elite" || p.Status == "Pinned");
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagName = tag.Trim();
            query = query.Where(p => _db.TagPosts.Any(tp =>
                tp.PostID == p.PostID &&
                tp.Tag != null &&
                tp.Tag.TagName == tagName));
        }

        List<Post> posts;
        if (sort == "hot")
        {
            var candidates = await query.ToListAsync();
            await RefreshHeatScoresAsync(candidates);
            posts = candidates
                .OrderByDescending(p => p.Status == "Pinned" ? 1 : 0)
                .ThenByDescending(p => p.HeatScore)
                .ThenByDescending(p => p.CreateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            posts = await query
                .OrderByDescending(p => p.Status == "Pinned" ? 1 : 0)
                .ThenByDescending(p => p.CreateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        return Ok(await MapPostListAsync(posts));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<PostDetailResponse>> GetById(int id)
    {
        var post = await _db.Posts.Include(p => p.User).Include(p => p.Forum)
            .FirstOrDefaultAsync(p => p.PostID == id);
        if (post is null || post.Status == "Deleted")
            return NotFound();
        if (!IsPublicPostStatus(post.Status) && !await CanViewRestrictedPostAsync(post))
            return NotFound();

        return Ok(await MapPostDetailAsync(post, incrementView: true));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<PostDetailResponse>> Create([FromBody] CreatePostRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!User.IsInRole("Admin") && !HasPermission("posts.create"))
            return Forbid();

        if (!await _creditService.CanPerformAsync(userId, "post"))
            return BadRequest(new { message = "信用分不足或账号不可用，暂不能发帖" });

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return Forbid();

        var forumExists = await _db.Forums.AnyAsync(f => f.ForumID == request.ForumID && f.Status == "Active");
        if (!forumExists)
            return BadRequest(new { message = "论坛不存在或不可用" });

        var hitWord = FindSensitiveWord(request.Title, request.Content);
        var post = new Post
        {
            ForumID = request.ForumID,
            Title = request.Title.Trim(),
            Content = request.Content,
            ImageUrls = SerializeImageUrls(request.ImageUrls),
            UserID = userId,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
            Status = hitWord == null ? "Active" : "PendingReview",
            LikeCount = 0,
            ViewCount = 0,
            HeatScore = 0
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        await ReplacePostTagsAsync(post.PostID, request.TagNames);
        await CreateMentionNotificationsAsync(userId, request.Content, "帖子提及", $"在帖子《{post.Title}》中提到了你");
        await _db.SaveChangesAsync();

        if (hitWord != null)
        {
            _db.AuditRecords.Add(new AuditRecord
            {
                TargetType = "Post",
                TargetID = post.PostID,
                TriggerWord = hitWord,
                Status = "Pending",
                CreateTime = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetById), new { id = post.PostID }, await MapPostDetailAsync(post));
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<PostDetailResponse>> Update(int id, [FromBody] UpdatePostRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var isModerator = User.IsInRole("Admin") || User.IsInRole("Moderator") || HasPermission("posts.delete");

        if (post.UserID != userId && !isModerator)
            return Forbid();

        var hitWord = FindSensitiveWord(request.Title, request.Content);
        post.Title = request.Title.Trim();
        post.Content = request.Content;
        post.ImageUrls = SerializeImageUrls(request.ImageUrls);
        post.UpdateTime = DateTime.Now;
        post.Status = hitWord == null ? post.Status : "PendingReview";

        await _db.SaveChangesAsync();
        await ReplacePostTagsAsync(post.PostID, request.TagNames);
        await CreateMentionNotificationsAsync(userId, request.Content, "帖子提及", $"在帖子《{post.Title}》中提到了你");
        await _db.SaveChangesAsync();

        if (hitWord != null)
        {
            _db.AuditRecords.Add(new AuditRecord
            {
                TargetType = "Post",
                TargetID = post.PostID,
                TriggerWord = hitWord,
                Status = "Pending",
                CreateTime = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        return Ok(await MapPostDetailAsync(post));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> Delete(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var canDelete = User.IsInRole("Admin") ||
            User.IsInRole("Moderator") ||
            HasPermission("posts.delete") ||
            await _db.ForumManagers.AnyAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId);

        if (post.UserID != userId && !canDelete)
            return Forbid();

        post.Status = "Deleted";
        post.UpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }

    [HttpPost("{id}/status")]
    [Authorize]
    public async Task<ActionResult> ChangeStatus(int id, [FromBody] ChangePostStatusRequest request)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "pin" and not "unpin" and not "elite" and not "unelite" and not "ban" and not "restore" and not "delete" and not "approve" and not "reject")
            return BadRequest(new { message = "状态动作不合法" });

        var canModerate = User.IsInRole("Admin") ||
            User.IsInRole("Moderator") ||
            await _db.ForumManagers.AnyAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId) ||
            CanRunPostStatusAction(action);

        var ownerAction = post.UserID == userId && action is "delete" or "restore";
        if (!canModerate && !ownerAction)
            return Forbid();

        post.Status = action switch
        {
            "pin" => "Pinned",
            "unpin" => "Active",
            "elite" => "Elite",
            "unelite" => "Active",
            "ban" => "Banned",
            "restore" => "Active",
            "delete" => "Deleted",
            "approve" => "Active",
            "reject" => "Banned",
            _ => post.Status
        };
        post.UpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new { message = "状态已更新", status = post.Status });
    }

    private bool CanRunPostStatusAction(string action)
    {
        return action switch
        {
            "pin" or "unpin" => HasPermission("posts.top"),
            "elite" or "unelite" => HasPermission("posts.elite"),
            "ban" or "approve" or "reject" => HasPermission("posts.moderate"),
            "delete" => HasPermission("posts.delete"),
            "restore" => HasPermission("posts.edit") || HasPermission("posts.moderate"),
            _ => false
        };
    }

    private bool HasPermission(string permission)
    {
        return User.Claims.Any(c => c.Type == "Permission" &&
            string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private bool CanViewModerationStatus()
    {
        return User.Identity?.IsAuthenticated == true &&
            (User.IsInRole("Admin") ||
             User.IsInRole("Moderator") ||
             HasPermission("posts.moderate") ||
             HasPermission("posts.delete") ||
             HasPermission("posts.edit"));
    }

    private async Task<bool> CanViewRestrictedPostAsync(Post post)
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return false;

        return post.UserID == userId.Value ||
            CanViewModerationStatus() ||
            await _db.ForumManagers.AnyAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId.Value);
    }

    private static bool IsPublicPostStatus(string? status)
    {
        return status is "Active" or "Elite" or "Pinned";
    }

    [HttpPost("{id}/like")]
    [Authorize]
    public async Task<ActionResult> Like(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var post = await _db.Posts.FindAsync(id);
        if (post == null || post.Status == "Deleted")
            return NotFound();
        if (!IsPublicPostStatus(post.Status))
            return BadRequest(new { message = "当前帖子不可点赞" });

        var exists = await _db.PostLikes.AnyAsync(l => l.PostID == id && l.UserID == userId);
        if (!exists)
        {
            _db.PostLikes.Add(new PostLike { PostID = id, UserID = userId, CreateTime = DateTime.Now });
            post.LikeCount = (post.LikeCount ?? 0) + 1;
            post.HeatScore = CalculateHeatScore(post, await _db.PostComments.CountAsync(c => c.PostID == id && c.Status != "Deleted"));
            await _db.SaveChangesAsync();
        }

        return Ok(new { liked = true, likeCount = post.LikeCount ?? 0 });
    }

    [HttpDelete("{id}/like")]
    [Authorize]
    public async Task<ActionResult> Unlike(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();

        var like = await _db.PostLikes.FirstOrDefaultAsync(l => l.PostID == id && l.UserID == userId);
        if (like != null)
        {
            _db.PostLikes.Remove(like);
            post.LikeCount = Math.Max(0, (post.LikeCount ?? 0) - 1);
            post.HeatScore = CalculateHeatScore(post, await _db.PostComments.CountAsync(c => c.PostID == id && c.Status != "Deleted"));
            await _db.SaveChangesAsync();
        }

        return Ok(new { liked = false, likeCount = post.LikeCount ?? 0 });
    }

    [HttpGet("{postId}/comments")]
    [AllowAnonymous]
    public async Task<ActionResult<List<CommentResponse>>> GetComments(int postId)
    {
        var post = await _db.Posts.FindAsync(postId);
        if (post == null || post.Status == "Deleted")
            return NotFound();
        if (!IsPublicPostStatus(post.Status) && !await CanViewRestrictedPostAsync(post))
            return NotFound();

        var userId = TryGetCurrentUserId();
        var canViewRestrictedComments = CanViewModerationStatus() ||
            (userId.HasValue && await _db.ForumManagers.AnyAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId.Value));

        var comments = await _db.PostComments
            .Include(c => c.User)
            .Where(c => c.PostID == postId &&
                c.Status != "Deleted" &&
                c.Status != "Banned" &&
                (c.Status == "Active" || (userId.HasValue && c.UserID == userId.Value) || canViewRestrictedComments))
            .OrderBy(c => c.CreateTime)
            .ToListAsync();

        return Ok(BuildCommentTree(comments));
    }

    [HttpPost("{postId}/comments")]
    [Authorize]
    public async Task<ActionResult<CommentResponse>> CreateComment(int postId, [FromBody] CreateCommentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!await _creditService.CanPerformAsync(userId, "comment"))
            return BadRequest(new { message = "信用分不足或账号不可用，暂不能评论" });

        var post = await _db.Posts.FindAsync(postId);
        if (post == null || post.Status is "Deleted" or "Banned")
            return NotFound();
        if (!IsPublicPostStatus(post.Status))
            return BadRequest(new { message = "当前帖子不可评论" });

        if (request.ParentCommentID.HasValue)
        {
            var parentExists = await _db.PostComments.AnyAsync(c =>
                c.CommentID == request.ParentCommentID.Value &&
                c.PostID == postId &&
                c.Status != "Deleted");
            if (!parentExists)
                return BadRequest(new { message = "父评论不存在" });
        }

        var hitWord = FindSensitiveWord("", request.Content);
        var comment = new PostComment
        {
            PostID = postId,
            UserID = userId,
            ParentCommentID = request.ParentCommentID,
            Content = request.Content,
            Status = hitWord == null ? "Active" : "PendingReview",
            CreateTime = DateTime.Now
        };

        _db.PostComments.Add(comment);
        post.HeatScore = CalculateHeatScore(post, await _db.PostComments.CountAsync(c => c.PostID == postId && c.Status != "Deleted") + 1);
        await _db.SaveChangesAsync();

        if (hitWord != null)
        {
            _db.AuditRecords.Add(new AuditRecord
            {
                TargetType = "Comment",
                TargetID = comment.CommentID,
                TriggerWord = hitWord,
                Status = "Pending",
                CreateTime = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        await CreateMentionNotificationsAsync(userId, request.Content, "评论提及", $"在帖子《{post.Title}》的评论中提到了你");
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        return Ok(new CommentResponse
        {
            CommentID = comment.CommentID,
            Content = comment.Content ?? "",
            Status = comment.Status ?? "",
            CreateTime = comment.CreateTime,
            UserID = comment.UserID,
            Username = user?.Username ?? "",
            ParentCommentID = comment.ParentCommentID
        });
    }

    [HttpDelete("/api/comments/{commentId}")]
    [Authorize]
    public async Task<ActionResult> DeleteComment(int commentId)
    {
        var comment = await _db.PostComments
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.CommentID == commentId);
        if (comment == null || comment.Status == "Deleted")
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var canDelete = comment.UserID == userId ||
            User.IsInRole("Admin") ||
            User.IsInRole("Moderator") ||
            HasPermission("posts.delete") ||
            await _db.ForumManagers.AnyAsync(fm =>
                comment.Post != null &&
                fm.ForumID == comment.Post.ForumID &&
                fm.UserID == userId);

        if (!canDelete)
            return Forbid();

        comment.Status = "Deleted";
        if (comment.Post != null)
        {
            var commentCount = await _db.PostComments.CountAsync(c =>
                c.PostID == comment.PostID &&
                c.Status != "Deleted" &&
                c.CommentID != commentId);
            comment.Post.HeatScore = CalculateHeatScore(comment.Post, commentCount);
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "评论已删除" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<List<PostListItemResponse>>> GetMyPosts()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var posts = await _db.Posts
            .Include(p => p.User)
            .Include(p => p.Forum)
            .Where(p => p.UserID == userId && p.Status != "Deleted")
            .OrderByDescending(p => p.CreateTime)
            .ToListAsync();

        return Ok(await MapPostListAsync(posts));
    }

    private async Task<List<PostListItemResponse>> MapPostListAsync(IEnumerable<Post> posts)
    {
        var postList = posts.ToList();
        var postIds = postList.Select(p => p.PostID).ToList();
        var tagRows = await _db.TagPosts
            .Include(tp => tp.Tag)
            .Where(tp => postIds.Contains(tp.PostID))
            .ToListAsync();
        var commentCounts = await _db.PostComments
            .Where(c => c.PostID.HasValue && postIds.Contains(c.PostID.Value) && c.Status != "Deleted")
            .GroupBy(c => c.PostID!.Value)
            .Select(g => new { PostID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PostID, x => x.Count);
        var userId = TryGetCurrentUserId();
        var likedPostIdList = userId.HasValue
            ? await _db.PostLikes
                .Where(l => l.UserID == userId.Value && l.PostID.HasValue && postIds.Contains(l.PostID.Value))
                .Select(l => l.PostID!.Value)
                .ToListAsync()
            : new List<int>();
        var likedPostIds = likedPostIdList.ToHashSet();
        var favoritedPostIdList = userId.HasValue
            ? await _db.FolderPosts
                .Where(fp =>
                    postIds.Contains(fp.PostID) &&
                    fp.Folder != null &&
                    fp.Folder.UserID == userId.Value)
                .Select(fp => fp.PostID)
                .ToListAsync()
            : new List<int>();
        var favoritedPostIds = favoritedPostIdList.ToHashSet();

        return postList.Select(p => new PostListItemResponse
        {
            PostID = p.PostID,
            Title = p.Title ?? "",
            ContentPreview = BuildPreview(p.Content),
            HeatScore = p.HeatScore ?? 0,
            LikeCount = p.LikeCount ?? 0,
            ViewCount = p.ViewCount ?? 0,
            CommentCount = commentCounts.TryGetValue(p.PostID, out var count) ? count : 0,
            Status = p.Status ?? "",
            CreateTime = p.CreateTime,
            UpdateTime = p.UpdateTime,
            UserID = p.UserID,
            Username = p.User?.Username ?? "",
            ForumID = p.ForumID,
            ForumName = p.Forum?.ForumName ?? "",
            Tags = tagRows
                .Where(t => t.PostID == p.PostID && t.Tag?.TagName != null)
                .Select(t => t.Tag!.TagName!)
                .ToList(),
            ImageUrls = DeserializeImageUrls(p.ImageUrls),
            IsLiked = likedPostIds.Contains(p.PostID),
            IsFavorited = favoritedPostIds.Contains(p.PostID)
        }).ToList();
    }

    private async Task<PostDetailResponse> MapPostDetailAsync(Post post, bool incrementView = false)
    {
        if (incrementView)
        {
            post.ViewCount = (post.ViewCount ?? 0) + 1;
            post.HeatScore = CalculateHeatScore(
                post,
                await _db.PostComments.CountAsync(c => c.PostID == post.PostID && c.Status != "Deleted"));
            await _db.SaveChangesAsync();
        }

        var listItem = (await MapPostListAsync(new[] { post })).Single();
        return new PostDetailResponse
        {
            PostID = listItem.PostID,
            Title = listItem.Title,
            ContentPreview = listItem.ContentPreview,
            Content = post.Content ?? "",
            HeatScore = listItem.HeatScore,
            LikeCount = listItem.LikeCount,
            ViewCount = listItem.ViewCount,
            CommentCount = listItem.CommentCount,
            Status = listItem.Status,
            CreateTime = listItem.CreateTime,
            UpdateTime = listItem.UpdateTime,
            UserID = listItem.UserID,
            Username = listItem.Username,
            ForumID = listItem.ForumID,
            ForumName = listItem.ForumName,
            Tags = listItem.Tags,
            ImageUrls = listItem.ImageUrls,
            IsLiked = listItem.IsLiked,
            IsFavorited = listItem.IsFavorited
        };
    }

    private async Task RefreshHeatScoresAsync(List<Post> posts)
    {
        if (posts.Count == 0)
            return;

        var postIds = posts.Select(p => p.PostID).ToList();
        var commentCounts = await _db.PostComments
            .Where(c => c.PostID.HasValue && postIds.Contains(c.PostID.Value) && c.Status != "Deleted")
            .GroupBy(c => c.PostID!.Value)
            .Select(g => new { PostID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PostID, x => x.Count);

        var changed = false;
        foreach (var post in posts)
        {
            var nextScore = CalculateHeatScore(
                post,
                commentCounts.TryGetValue(post.PostID, out var count) ? count : 0);
            if (post.HeatScore != nextScore)
            {
                post.HeatScore = nextScore;
                changed = true;
            }
        }

        if (changed)
            await _db.SaveChangesAsync();
    }

    private async Task ReplacePostTagsAsync(int postId, IEnumerable<string> tagNames)
    {
        var normalized = tagNames
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var oldTags = await _db.TagPosts.Where(tp => tp.PostID == postId).ToListAsync();
        _db.TagPosts.RemoveRange(oldTags);

        foreach (var name in normalized)
        {
            var tag = await _db.PostTags.FirstOrDefaultAsync(t => t.TagName == name);
            if (tag == null)
            {
                tag = new PostTag { TagName = name, CreateTime = DateTime.Now };
                _db.PostTags.Add(tag);
                await _db.SaveChangesAsync();
            }

            _db.TagPosts.Add(new TagPost { PostID = postId, TagID = tag.TagID });
        }

        await _db.SaveChangesAsync();
    }

    private int? TryGetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static string BuildPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "";

        return content.Length <= 120 ? content : content[..120] + "...";
    }

    private static string? FindSensitiveWord(string title, string content)
    {
        var text = $"{title}\n{content}";
        return SensitiveWords.FirstOrDefault(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static string SerializeImageUrls(IEnumerable<string> urls)
    {
        var normalized = urls
            .Select(url => url.Trim())
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        return JsonSerializer.Serialize(normalized);
    }

    private static List<string> DeserializeImageUrls(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task CreateMentionNotificationsAsync(int senderId, string content, string title, string notificationContent)
    {
        var mentionedNames = Regex.Matches(content, @"@([\p{L}\p{N}_\-.]{2,50})")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (mentionedNames.Count == 0)
            return;

        var mentionedUsers = await _db.Users
            .Where(u => u.UserID != senderId && u.Username != null && mentionedNames.Contains(u.Username))
            .ToListAsync();

        foreach (var user in mentionedUsers)
        {
            _db.Notifications.Add(new Notification
            {
                UserID = user.UserID,
                Title = title,
                Content = notificationContent,
                CreateTime = DateTime.Now
            });
        }
    }

    private static int CalculateHeatScore(Post post, int commentCount)
    {
        var ageHours = Math.Max(0, (DateTime.Now - (post.CreateTime ?? DateTime.Now)).TotalHours);
        var decay = (int)Math.Floor(ageHours / 24);
        var bonus = post.Status switch
        {
            "Pinned" => 1000,
            "Elite" => 200,
            _ => 0
        };

        return Math.Max(0, (post.ViewCount ?? 0) + (post.LikeCount ?? 0) * 5 + commentCount * 8 + bonus - decay);
    }

    private static List<CommentResponse> BuildCommentTree(List<PostComment> comments)
    {
        var nodes = comments.ToDictionary(c => c.CommentID, c => new CommentResponse
        {
            CommentID = c.CommentID,
            Content = c.Content ?? "",
            Status = c.Status ?? "",
            CreateTime = c.CreateTime,
            UserID = c.UserID,
            Username = c.User?.Username ?? "",
            ParentCommentID = c.ParentCommentID
        });

        var roots = new List<CommentResponse>();
        foreach (var comment in comments)
        {
            var node = nodes[comment.CommentID];
            if (comment.ParentCommentID.HasValue && nodes.TryGetValue(comment.ParentCommentID.Value, out var parent))
            {
                parent.Replies.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }
}

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TagsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<string>>> GetTags([FromQuery] string? keyword)
    {
        var query = _db.PostTags.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(t => t.TagName != null && t.TagName.Contains(trimmed));
        }

        return Ok(await query
            .OrderBy(t => t.TagName)
            .Select(t => t.TagName ?? "")
            .Where(t => t != "")
            .Take(20)
            .ToListAsync());
    }

    [HttpPost("suggest")]
    [AllowAnonymous]
    public async Task<ActionResult<List<string>>> Suggest([FromBody] TagSuggestRequest request)
    {
        var existingTags = await _db.PostTags
            .Select(t => t.TagName ?? "")
            .Where(t => t != "")
            .ToListAsync();

        var text = $"{request.Title} {request.Content}";
        var suggestions = existingTags
            .Where(tag => text.Contains(tag, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (suggestions.Count == 0)
        {
            suggestions = text
                .Split([' ', ',', '.', '，', '。', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length >= 2 && word.Length <= 20)
                .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(5)
                .ToList();
        }

        return Ok(suggestions);
    }
}

[ApiController]
[Route("api/favorite-folders")]
[Authorize]
public class FavoriteFoldersController : ControllerBase
{
    private readonly AppDbContext _db;

    public FavoriteFoldersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<FavoriteFolderResponse>>> GetFolders()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folders = await _db.FavoriteFolders
            .Where(f => f.UserID == userId)
            .Select(f => new FavoriteFolderResponse
            {
                FolderID = f.FolderID,
                FolderName = f.FolderName ?? "",
                CreateTime = f.CreateTime,
                PostCount = _db.FolderPosts.Count(fp => fp.FolderID == f.FolderID)
            })
            .ToListAsync();

        return Ok(folders);
    }

    [HttpPost]
    public async Task<ActionResult<FavoriteFolderResponse>> CreateFolder([FromBody] CreateFavoriteFolderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = new FavoriteFolder
        {
            FolderName = request.FolderName.Trim(),
            CreateTime = DateTime.Now,
            UserID = userId
        };

        _db.FavoriteFolders.Add(folder);
        await _db.SaveChangesAsync();

        return Ok(new FavoriteFolderResponse
        {
            FolderID = folder.FolderID,
            FolderName = folder.FolderName ?? "",
            CreateTime = folder.CreateTime,
            PostCount = 0
        });
    }

    [HttpPut("{folderId}")]
    public async Task<ActionResult<FavoriteFolderResponse>> UpdateFolder(int folderId, [FromBody] CreateFavoriteFolderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        folder.FolderName = request.FolderName.Trim();
        await _db.SaveChangesAsync();

        return Ok(new FavoriteFolderResponse
        {
            FolderID = folder.FolderID,
            FolderName = folder.FolderName ?? "",
            CreateTime = folder.CreateTime,
            PostCount = await _db.FolderPosts.CountAsync(fp => fp.FolderID == folder.FolderID)
        });
    }

    [HttpDelete("{folderId}")]
    public async Task<ActionResult> DeleteFolder(int folderId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        var folderPosts = await _db.FolderPosts.Where(fp => fp.FolderID == folderId).ToListAsync();
        _db.FolderPosts.RemoveRange(folderPosts);
        _db.FavoriteFolders.Remove(folder);
        await _db.SaveChangesAsync();

        return Ok(new { message = "收藏夹已删除" });
    }

    [HttpPost("{folderId}/posts/{postId}")]
    public async Task<ActionResult> AddPost(int folderId, int postId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        var postExists = await _db.Posts.AnyAsync(p => p.PostID == postId && p.Status != "Deleted");
        if (!postExists)
            return NotFound(new { message = "帖子不存在" });

        var exists = await _db.FolderPosts.AnyAsync(fp => fp.FolderID == folderId && fp.PostID == postId);
        if (!exists)
        {
            _db.FolderPosts.Add(new FolderPost { FolderID = folderId, PostID = postId });
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "已收藏" });
    }

    [HttpGet("{folderId}/posts")]
    public async Task<ActionResult<List<PostListItemResponse>>> GetFolderPosts(int folderId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        var posts = await _db.FolderPosts
            .Include(fp => fp.Post)
            .ThenInclude(p => p!.User)
            .Include(fp => fp.Post)
            .ThenInclude(p => p!.Forum)
            .Where(fp => fp.FolderID == folderId && fp.Post != null && fp.Post.Status != "Deleted")
            .Select(fp => fp.Post!)
            .ToListAsync();

        var postIds = posts.Select(p => p.PostID).ToList();
        var tagRows = await _db.TagPosts
            .Include(tp => tp.Tag)
            .Where(tp => postIds.Contains(tp.PostID))
            .ToListAsync();

        return Ok(posts.Select(p => new PostListItemResponse
        {
            PostID = p.PostID,
            Title = p.Title ?? "",
            ContentPreview = string.IsNullOrWhiteSpace(p.Content)
                ? ""
                : p.Content.Length <= 120 ? p.Content : p.Content[..120] + "...",
            HeatScore = p.HeatScore ?? 0,
            LikeCount = p.LikeCount ?? 0,
            ViewCount = p.ViewCount ?? 0,
            Status = p.Status ?? "",
            CreateTime = p.CreateTime,
            UpdateTime = p.UpdateTime,
            UserID = p.UserID,
            Username = p.User?.Username ?? "",
            ForumID = p.ForumID,
            ForumName = p.Forum?.ForumName ?? "",
            Tags = tagRows
                .Where(t => t.PostID == p.PostID && t.Tag?.TagName != null)
                .Select(t => t.Tag!.TagName!)
                .ToList()
        }).ToList());
    }

    [HttpDelete("{folderId}/posts/{postId}")]
    public async Task<ActionResult> RemovePost(int folderId, int postId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        var folderPost = await _db.FolderPosts.FirstOrDefaultAsync(fp => fp.FolderID == folderId && fp.PostID == postId);
        if (folderPost != null)
        {
            _db.FolderPosts.Remove(folderPost);
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "已取消收藏" });
    }
}

[ApiController]
[Route("api/audits/posts")]
[RequirePermission("posts.moderate")]
public class PostAuditsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;

    public PostAuditsController(AppDbContext db, ICreditService creditService)
    {
        _db = db;
        _creditService = creditService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditRecordResponse>>> GetPending()
    {
        var audits = await _db.AuditRecords
            .Where(a => a.Status == "Pending")
            .OrderBy(a => a.CreateTime)
            .ToListAsync();
        var postIds = audits
            .Where(a => a.TargetID.HasValue && NormalizeAuditTargetType(a.TargetType) == "Post")
            .Select(a => a.TargetID!.Value)
            .ToList();
        var commentIds = audits
            .Where(a => a.TargetID.HasValue && NormalizeAuditTargetType(a.TargetType) == "Comment")
            .Select(a => a.TargetID!.Value)
            .ToList();
        var posts = await _db.Posts
            .Include(p => p.User)
            .Include(p => p.Forum)
            .Where(p => postIds.Contains(p.PostID))
            .ToListAsync();
        var comments = await _db.PostComments
            .Include(c => c.User)
            .Where(c => commentIds.Contains(c.CommentID))
            .ToListAsync();

        return Ok(audits.Select(a =>
        {
            var targetType = NormalizeAuditTargetType(a.TargetType);
            var post = targetType == "Post" ? posts.FirstOrDefault(p => p.PostID == a.TargetID) : null;
            var comment = targetType == "Comment" ? comments.FirstOrDefault(c => c.CommentID == a.TargetID) : null;
            return new AuditRecordResponse
            {
                AuditID = a.AuditID,
                TargetType = targetType,
                TargetID = a.TargetID,
                TriggerWord = a.TriggerWord ?? "",
                Status = a.Status ?? "",
                CreateTime = a.CreateTime,
                AuditorID = a.AuditorID,
                Post = post == null ? null : new PostListItemResponse
                {
                    PostID = post.PostID,
                    Title = post.Title ?? "",
                    ContentPreview = post.Content ?? "",
                    HeatScore = post.HeatScore ?? 0,
                    LikeCount = post.LikeCount ?? 0,
                    ViewCount = post.ViewCount ?? 0,
                    Status = post.Status ?? "",
                    CreateTime = post.CreateTime,
                    UpdateTime = post.UpdateTime,
                    UserID = post.UserID,
                    Username = post.User?.Username ?? "",
                    ForumID = post.ForumID,
                    ForumName = post.Forum?.ForumName ?? ""
                },
                Comment = comment == null ? null : new CommentResponse
                {
                    CommentID = comment.CommentID,
                    Content = comment.Content ?? "",
                    Status = comment.Status ?? "",
                    CreateTime = comment.CreateTime,
                    UserID = comment.UserID,
                    Username = comment.User?.Username ?? "",
                    ParentCommentID = comment.ParentCommentID
                }
            };
        }).ToList());
    }

    [HttpPost("{auditId}/approve")]
    public async Task<ActionResult> Approve(int auditId)
    {
        return await CompleteAudit(auditId, "Approved", "Active");
    }

    [HttpPost("{auditId}/reject")]
    public async Task<ActionResult> Reject(int auditId)
    {
        return await CompleteAudit(auditId, "Rejected", "Banned");
    }

    private async Task<ActionResult> CompleteAudit(int auditId, string auditStatus, string postStatus)
    {
        var audit = await _db.AuditRecords.FindAsync(auditId);
        if (audit == null)
            return NotFound();

        var targetType = NormalizeAuditTargetType(audit.TargetType);
        if (targetType == "Post")
        {
            var post = audit.TargetID.HasValue ? await _db.Posts.FindAsync(audit.TargetID.Value) : null;
            if (post != null)
            {
                post.Status = postStatus;
                post.UpdateTime = DateTime.Now;

                if (auditStatus == "Rejected" && post.UserID.HasValue)
                {
                    await _creditService.AddCreditAsync(post.UserID.Value, -15, $"帖子审核拒绝：{post.Title}");
                    _db.Notifications.Add(new Notification
                    {
                        UserID = post.UserID.Value,
                        Title = "帖子审核未通过",
                        Content = $"你的帖子《{post.Title}》因命中敏感内容未通过审核，信用分 -15",
                        CreateTime = DateTime.Now
                    });
                }
                else if (auditStatus == "Approved" && post.UserID.HasValue)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserID = post.UserID.Value,
                        Title = "帖子审核通过",
                        Content = $"你的帖子《{post.Title}》已通过审核",
                        CreateTime = DateTime.Now
                    });
                }
            }
        }
        else
        {
            var comment = audit.TargetID.HasValue ? await _db.PostComments.FindAsync(audit.TargetID.Value) : null;
            if (comment != null)
            {
                comment.Status = auditStatus == "Approved" ? "Active" : "Banned";

                if (auditStatus == "Rejected" && comment.UserID.HasValue)
                {
                    await _creditService.AddCreditAsync(comment.UserID.Value, -10, "评论审核拒绝");
                    _db.Notifications.Add(new Notification
                    {
                        UserID = comment.UserID.Value,
                        Title = "评论审核未通过",
                        Content = "你的评论因命中敏感内容未通过审核，信用分 -10",
                        CreateTime = DateTime.Now
                    });
                }
                else if (auditStatus == "Approved" && comment.UserID.HasValue)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserID = comment.UserID.Value,
                        Title = "评论审核通过",
                        Content = "你的评论已通过审核",
                        CreateTime = DateTime.Now
                    });
                }
            }
        }

        audit.Status = auditStatus;
        audit.AuditorID = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        await _db.SaveChangesAsync();

        return Ok(new { message = "审核完成", status = auditStatus });
    }

    private static string NormalizeAuditTargetType(string? targetType)
    {
        return string.Equals(targetType, "Comment", StringComparison.OrdinalIgnoreCase) ? "Comment" : "Post";
    }
}

[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly AppDbContext _db;

    public WalletController(AppDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<ActionResult<WalletResponse>> GetMine()
    {
        var wallet = await GetOrCreateWalletAsync(CurrentUserId());
        await _db.SaveChangesAsync();
        return Ok(MapWallet(wallet));
    }

    [HttpPost("deposit")]
    public async Task<ActionResult<WalletResponse>> Deposit([FromBody] DepositWalletRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var wallet = await GetOrCreateWalletAsync(CurrentUserId());
        wallet.Balance = (wallet.Balance ?? 0) + request.Amount;
        await _db.SaveChangesAsync();

        return Ok(MapWallet(wallet));
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserID == userId);
        if (wallet != null)
            return wallet;

        wallet = new Wallet
        {
            UserID = userId,
            Balance = 0
        };
        _db.Wallets.Add(wallet);
        return wallet;
    }

    private static WalletResponse MapWallet(Wallet wallet)
    {
        return new WalletResponse
        {
            WalletID = wallet.WalletID,
            Balance = wallet.Balance ?? 0,
            UserID = wallet.UserID
        };
    }
}

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;

    public ProductsController(AppDbContext db, ICreditService creditService)
    {
        _db = db;
        _creditService = creditService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ProductResponse>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Products.Include(p => p.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);
        else
            query = query.Where(p => p.Status != "Inactive");

        var products = await query
            .OrderByDescending(p => p.PublishTime)
            .ToListAsync();

        return Ok(products.Select(MapProduct).ToList());
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var product = await _db.Products.Include(p => p.User).FirstOrDefaultAsync(p => p.ProductID == id);
        return product is null ? NotFound() : Ok(MapProduct(product));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] CreateProductRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!User.IsInRole("Admin") && !HasProductPermission("products.create"))
            return Forbid();

        if (!await _creditService.CanPerformAsync(userId, "product.publish"))
            return BadRequest(new { message = "信用分不足或账号不可用，暂不能发布商品" });

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return Forbid();

        var product = new Product
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            ImageUrls = SerializeImageUrls(request.ImageUrls),
            Price = request.Price,
            Stock = request.Stock,
            UserID = userId,
            PublishTime = DateTime.Now,
            Status = "Active"
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.ProductID }, MapProduct(product));
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<ProductResponse>> Update(int id, [FromBody] UpdateProductRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        if (!CanManageProduct(product, userId, "products.edit"))
            return Forbid();

        if (product.Status == "Sold")
            return BadRequest(new { message = "已售出商品不可编辑" });

        product.Title = request.Title.Trim();
        product.Description = request.Description;
        product.ImageUrls = SerializeImageUrls(request.ImageUrls);
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.Status = NormalizeProductStatus(request.Status);

        await _db.SaveChangesAsync();
        return Ok(MapProduct(product));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        if (!CanManageProduct(product, userId, "products.delete"))
            return Forbid();

        product.Status = "Inactive";
        await _db.SaveChangesAsync();
        return Ok(new { message = "已下架" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<List<ProductResponse>>> GetMyProducts()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var products = await _db.Products
            .Include(p => p.User)
            .Where(p => p.UserID == userId)
            .OrderByDescending(p => p.PublishTime)
            .ToListAsync();

        return Ok(products.Select(MapProduct).ToList());
    }

    [HttpPost("{id}/status")]
    [Authorize]
    public async Task<ActionResult> ChangeStatus(int id, [FromBody] ChangeProductStatusRequest request)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "publish" and not "lock" and not "sold" and not "inactive" and not "off-shelf" and not "restore")
            return BadRequest(new { message = "状态动作不合法" });

        var requiredPermission = action is "inactive" or "off-shelf" ? "products.delete" : "products.edit";
        if (!CanManageProduct(product, userId, requiredPermission))
            return Forbid();

        product.Status = action switch
        {
            "publish" => product.Stock > 0 ? "Active" : "Sold",
            "lock" => "Locked",
            "sold" => "Sold",
            "inactive" or "off-shelf" => "Inactive",
            "restore" => product.Stock > 0 ? "Active" : "Sold",
            _ => product.Status
        };

        await _db.SaveChangesAsync();
        return Ok(new { message = "状态已更新", status = product.Status });
    }

    private static ProductResponse MapProduct(Product product)
    {
        return new ProductResponse
        {
            ProductID = product.ProductID,
            Title = product.Title ?? "",
            Description = product.Description ?? "",
            Price = product.Price ?? 0,
            Stock = product.Stock ?? 0,
            Status = product.Status ?? "",
            PublishTime = product.PublishTime,
            UserID = product.UserID,
            SellerName = product.User?.Username ?? "",
            ImageUrls = DeserializeImageUrls(product.ImageUrls)
        };
    }

    private bool HasProductPermission(string permission)
    {
        return User.Claims.Any(c => c.Type == "Permission" &&
            string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private bool CanManageProduct(Product product, int userId, string permission)
    {
        return product.UserID == userId ||
            User.IsInRole("Admin") ||
            HasProductPermission(permission);
    }

    private static string NormalizeProductStatus(string status)
    {
        return status switch
        {
            "Active" or "Locked" or "Sold" or "Inactive" => status,
            _ => "Active"
        };
    }

    private static string SerializeImageUrls(IEnumerable<string> urls)
    {
        var normalized = urls
            .Select(url => url.Trim())
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        return JsonSerializer.Serialize(normalized);
    }

    private static List<string> DeserializeImageUrls(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;

    public TransactionsController(AppDbContext db, ICreditService creditService)
    {
        _db = db;
        _creditService = creditService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<List<TransactionResponse>>> GetMyOrders()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var orders = await _db.Transactions
            .Include(t => t.Product)
            .ThenInclude(p => p!.User)
            .Include(t => t.User)
            .Where(t => t.UserID == userId)
            .OrderByDescending(t => t.CreateTime)
            .ToListAsync();

        return Ok(orders.Select(MapTransaction).ToList());
    }

    [HttpGet("sales")]
    public async Task<ActionResult<List<TransactionResponse>>> GetSales()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var orders = await _db.Transactions
            .Include(t => t.Product)
            .ThenInclude(p => p!.User)
            .Include(t => t.User)
            .Where(t => t.Product != null && t.Product.UserID == userId)
            .OrderByDescending(t => t.CreateTime)
            .ToListAsync();

        return Ok(orders.Select(MapTransaction).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> CreateOrder([FromBody] CreateTransactionRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!await _creditService.CanPerformAsync(userId, "order.create"))
            return BadRequest(new { message = "信用分不足或账号不可用，暂不能接单/下单" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        var product = await _db.Products
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.ProductID == request.ProductID);
        if (product == null || product.Status != "Active")
            return BadRequest(new { message = "商品不可下单" });

        if (product.UserID == userId)
            return BadRequest(new { message = "不能购买自己发布的商品" });

        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Product""
            SET ""stock"" = ""stock"" - 1,
                ""status"" = CASE WHEN ""stock"" - 1 <= 0 THEN 'Locked' ELSE ""status"" END
            WHERE ""productId"" = {request.ProductID}
              AND ""stock"" > 0
              AND ""status"" = 'Active'");

        if (affected != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "库存不足或商品已锁定" });
        }

        var order = new Transaction
        {
            TransactionAmount = product.Price ?? 0,
            TransactionStatus = "Pending",
            CreateTime = DateTime.Now,
            UserID = userId,
            ProductID = request.ProductID
        };

        _db.Transactions.Add(order);
        await _db.SaveChangesAsync();

        await CreateNotificationAsync(userId, "订单已创建", $"你已锁定商品：{product.Title}", order.TransactionID);
        if (product.UserID.HasValue)
            await CreateNotificationAsync(product.UserID.Value, "商品被下单", $"商品 {product.Title} 已被买家锁定", order.TransactionID);

        await dbTransaction.CommitAsync();

        order.Product = product;
        order.User = await _db.Users.FindAsync(userId);
        return Ok(MapTransaction(order));
    }

    [HttpPost("{id}/pay")]
    public async Task<ActionResult> Pay(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();
        if (order.TransactionStatus != "Pending")
            return BadRequest(new { message = "当前订单不可支付" });
        if (order.Product?.UserID == null)
            return BadRequest(new { message = "商品卖家不存在" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var buyerWallet = await GetOrCreateWalletAsync(order.UserID!.Value);
        await _db.SaveChangesAsync();

        var amount = order.TransactionAmount ?? 0;
        var debited = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" - {amount}
            WHERE ""walletId"" = {buyerWallet.WalletID}
              AND ""balance"" >= {amount}");

        if (debited != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "钱包余额不足" });
        }

        var payTime = DateTime.Now;
        var paid = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = 'Paid',
                ""payTime"" = {payTime}
            WHERE ""transactionId"" = {id}
              AND ""transactionStatus"" = 'Pending'");

        if (paid != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "当前订单不可支付" });
        }

        order.TransactionStatus = "Paid";
        order.PayTime = payTime;
        await CreateNotificationAsync(order.UserID.Value, "支付成功", $"订单 {id} 已支付，等待确认收货", id);
        if (order.Product?.UserID.HasValue == true)
            await CreateNotificationAsync(order.Product.UserID.Value, "买家已支付", $"订单 {id} 已支付", id);
        await _db.SaveChangesAsync();
        await _db.Entry(buyerWallet).ReloadAsync();
        await dbTransaction.CommitAsync();

        return Ok(new { message = "支付成功，资金已进入担保账户", status = order.TransactionStatus, walletBalance = buyerWallet.Balance ?? 0 });
    }

    [HttpPost("{id}/confirm-receipt")]
    public async Task<ActionResult> ConfirmReceipt(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();
        if (order.TransactionStatus != "Paid")
            return BadRequest(new { message = "当前订单不可确认收货" });
        if (order.Product?.UserID == null)
            return BadRequest(new { message = "商品卖家不存在" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var sellerWallet = await GetOrCreateWalletAsync(order.Product.UserID.Value);
        await _db.SaveChangesAsync();

        var completed = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = 'Completed'
            WHERE ""transactionId"" = {id}
              AND ""transactionStatus"" = 'Paid'");

        if (completed != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "当前订单不可确认收货" });
        }

        var amount = order.TransactionAmount ?? 0;
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" + {amount}
            WHERE ""walletId"" = {sellerWallet.WalletID}");

        order.TransactionStatus = "Completed";
        if (order.Product != null && (order.Product.Stock ?? 0) <= 0)
            order.Product.Status = "Sold";
        await ArchiveOrderMessagesAsync(id);

        await CreateNotificationAsync(order.UserID!.Value, "交易完成", $"订单 {id} 已完成", id);
        if (order.Product?.UserID.HasValue == true)
            await CreateNotificationAsync(order.Product.UserID.Value, "交易完成", $"订单 {id} 已完成，可结算资金", id);

        await _db.SaveChangesAsync();
        await _db.Entry(sellerWallet).ReloadAsync();
        await dbTransaction.CommitAsync();
        return Ok(new { message = "确认收货成功，资金已结算给卖家", status = order.TransactionStatus });
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(int id)
    {
        var order = await FindOwnedOrderAsync(id);
        if (order == null)
            return NotFound();
        if (order.TransactionStatus != "Pending")
            return BadRequest(new { message = "只有待支付订单可以取消" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var cancelled = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = 'Cancelled'
            WHERE ""transactionId"" = {id}
              AND ""transactionStatus"" = 'Pending'");

        if (cancelled != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "只有待支付订单可以取消" });
        }

        order.TransactionStatus = "Cancelled";
        if (order.ProductID.HasValue)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Product""
                SET ""stock"" = ""stock"" + 1,
                    ""status"" = 'Active'
                WHERE ""productId"" = {order.ProductID.Value}");
        }
        await ArchiveOrderMessagesAsync(id);
        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return Ok(new { message = "订单已取消", status = order.TransactionStatus });
    }

    [HttpGet("{id}/messages")]
    public async Task<ActionResult<List<OrderMessageResponse>>> GetOrderMessages(int id)
    {
        var order = await FindParticipatingOrderAsync(id);
        if (order == null)
            return NotFound();

        var messages = await _db.OrderMessages
            .Include(m => m.Sender)
            .Where(m => m.TransactionID == id)
            .OrderBy(m => m.SendTime)
            .ToListAsync();

        return Ok(messages.Select(MapOrderMessage).ToList());
    }

    [HttpPost("{id}/messages")]
    public async Task<ActionResult<OrderMessageResponse>> SendOrderMessage(int id, [FromBody] CreateOrderMessageRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var order = await FindParticipatingOrderAsync(id);
        if (order == null)
            return NotFound();

        if (IsArchivedStatus(order.TransactionStatus))
            return BadRequest(new { message = "交易已结束，留言板已归档只读" });

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var message = new OrderMessage
        {
            TransactionID = id,
            SenderID = userId,
            Content = request.Content,
            SendTime = DateTime.Now,
            IsArchived = "0"
        };

        _db.OrderMessages.Add(message);
        await _db.SaveChangesAsync();

        message.Sender = await _db.Users.FindAsync(userId);
        return Ok(MapOrderMessage(message));
    }

    private async Task<Transaction?> FindOwnedOrderAsync(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        return await _db.Transactions
            .Include(t => t.Product)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TransactionID == id && t.UserID == userId);
    }

    private async Task<Transaction?> FindParticipatingOrderAsync(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        return await _db.Transactions
            .Include(t => t.Product)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TransactionID == id &&
                (t.UserID == userId || (t.Product != null && t.Product.UserID == userId)));
    }

    private async Task CreateNotificationAsync(int userId, string title, string content, int transactionId)
    {
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Title = title,
            Content = content,
            TransactionID = transactionId,
            CreateTime = DateTime.Now
        });
        await Task.CompletedTask;
    }

    private async Task ArchiveOrderMessagesAsync(int transactionId)
    {
        var messages = await _db.OrderMessages
            .Where(m => m.TransactionID == transactionId && m.IsArchived != "1")
            .ToListAsync();

        foreach (var message in messages)
            message.IsArchived = "1";
    }

    private async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserID == userId);
        if (wallet != null)
            return wallet;

        wallet = new Wallet
        {
            UserID = userId,
            Balance = 0
        };
        _db.Wallets.Add(wallet);
        return wallet;
    }

    private static TransactionResponse MapTransaction(Transaction transaction)
    {
        return new TransactionResponse
        {
            TransactionID = transaction.TransactionID,
            TransactionAmount = transaction.TransactionAmount ?? 0,
            TransactionStatus = transaction.TransactionStatus ?? "",
            CreateTime = transaction.CreateTime,
            PayTime = transaction.PayTime,
            UserID = transaction.UserID,
            ProductID = transaction.ProductID,
            ProductTitle = transaction.Product?.Title ?? "",
            BuyerName = transaction.User?.Username ?? "",
            SellerID = transaction.Product?.UserID,
            SellerName = transaction.Product?.User?.Username ?? ""
        };
    }

    private static bool IsArchivedStatus(string? status)
    {
        return status is "Completed" or "Cancelled" or "Refunded";
    }

    private static OrderMessageResponse MapOrderMessage(OrderMessage message)
    {
        return new OrderMessageResponse
        {
            OrderMessageID = message.OrderMessageID,
            Content = message.Content ?? "",
            SendTime = message.SendTime,
            IsArchived = message.IsArchived == "1",
            TransactionID = message.TransactionID,
            SenderID = message.SenderID,
            SenderName = message.Sender?.Username ?? ""
        };
    }
}

[ApiController]
[Route("api/disputes")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;

    public DisputesController(AppDbContext db, ICreditService creditService)
    {
        _db = db;
        _creditService = creditService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DisputeTicketResponse>>> GetDisputes()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var canViewAll = User.IsInRole("Admin") ||
            User.IsInRole("Moderator") ||
            User.IsInRole("Manager") ||
            HasAnyPermission("dashboard.view", "products.edit");

        var query = _db.DisputeTickets
            .Include(d => d.User)
            .Include(d => d.Arbitrator)
            .Include(d => d.ArbitrationResults)
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.User)
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.Product)
            .ThenInclude(p => p!.User)
            .AsQueryable();

        if (!canViewAll)
            query = query.Where(d => d.UserID == userId || d.ArbitratorID == userId);

        var disputes = await query.OrderByDescending(d => d.CreateTime).ToListAsync();
        return Ok(disputes.Select(MapDispute).ToList());
    }

    [HttpPost("transactions/{transactionId}")]
    public async Task<ActionResult<DisputeTicketResponse>> CreateDispute(int transactionId, [FromBody] CreateDisputeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var order = await _db.Transactions
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.TransactionID == transactionId);

        if (order == null)
            return NotFound(new { message = "订单不存在" });

        var isBuyer = order.UserID == userId;
        var isSeller = order.Product?.UserID == userId;
        if (!isBuyer && !isSeller)
            return Forbid();

        if (order.TransactionStatus != "Paid")
            return BadRequest(new { message = "只有已支付且未完成的订单可以发起纠纷" });

        var exists = await _db.DisputeTickets.AnyAsync(d => d.TransactionID == transactionId && d.Status == "Open");
        if (exists)
            return BadRequest(new { message = "该订单已有处理中的纠纷" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var disputed = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = 'Disputed'
            WHERE ""transactionId"" = {transactionId}
              AND ""transactionStatus"" = 'Paid'");

        if (disputed != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "只有已支付且未完成的订单可以发起纠纷" });
        }

        var arbitratorId = await PickArbitratorAsync();
        order.TransactionStatus = "Disputed";
        var dispute = new DisputeTicket
        {
            TransactionID = transactionId,
            UserID = userId,
            ArbitratorID = arbitratorId,
            Reason = request.Reason,
            Status = "Open",
            CreateTime = DateTime.Now,
            AssignTime = arbitratorId.HasValue ? DateTime.Now : null
        };

        _db.DisputeTickets.Add(dispute);
        if (arbitratorId.HasValue)
            await CreateNotificationAsync(arbitratorId.Value, "纠纷工单已分配", $"订单 {transactionId} 的纠纷已分配给你处理", transactionId);
        if (order.UserID.HasValue && order.UserID.Value != userId)
            await CreateNotificationAsync(order.UserID.Value, "订单纠纷已发起", $"订单 {transactionId} 已进入纠纷处理", transactionId);
        if (order.Product?.UserID.HasValue == true && order.Product.UserID.Value != userId)
            await CreateNotificationAsync(order.Product.UserID.Value, "订单纠纷已发起", $"订单 {transactionId} 已进入纠纷处理", transactionId);
        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        dispute.User = await _db.Users.FindAsync(userId);
        dispute.Arbitrator = arbitratorId.HasValue ? await _db.Users.FindAsync(arbitratorId.Value) : null;
        return Ok(MapDispute(dispute));
    }

    [HttpPost("{id}/resolve")]
    [RequirePermission("products.edit", "dashboard.view")]
    public async Task<ActionResult> Resolve(int id, [FromBody] ResolveDisputeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var dispute = await _db.DisputeTickets
            .Include(d => d.Transaction)
            .ThenInclude(t => t!.Product)
            .Include(d => d.User)
            .Include(d => d.Arbitrator)
            .Include(d => d.ArbitrationResults)
            .FirstOrDefaultAsync(d => d.TicketID == id);
        if (dispute == null)
            return NotFound();
        if (dispute.Status != "Open")
            return BadRequest(new { message = "该纠纷已处理" });
        if (dispute.Transaction == null || dispute.Transaction.Product?.UserID == null)
            return BadRequest(new { message = "订单或卖家不存在" });
        if (dispute.Transaction.TransactionStatus != "Disputed")
            return BadRequest(new { message = "订单当前不在纠纷处理中" });

        var buyerId = dispute.Transaction.UserID!.Value;
        var sellerId = dispute.Transaction.Product.UserID.Value;
        var amount = dispute.Transaction.TransactionAmount ?? 0;
        if (request.RefundAmount > amount)
            return BadRequest(new { message = "退款金额不能超过订单金额" });

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var finalStatus = request.RefundAmount > 0 ? "Refunded" : "Completed";
        var disputeUpdated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""DisputeTicket""
            SET ""status"" = 'Resolved'
            WHERE ""ticketId"" = {id}
              AND ""status"" = 'Open'");

        var orderUpdated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Transaction""
            SET ""transactionStatus"" = {finalStatus}
            WHERE ""transactionId"" = {dispute.Transaction.TransactionID}
              AND ""transactionStatus"" = 'Disputed'");

        if (disputeUpdated != 1 || orderUpdated != 1)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { message = "该纠纷或订单状态已变化，不能重复结算" });
        }

        var buyerWallet = await GetOrCreateWalletAsync(buyerId);
        var sellerWallet = await GetOrCreateWalletAsync(sellerId);
        await _db.SaveChangesAsync();

        var sellerAmount = amount - request.RefundAmount;
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" + {request.RefundAmount}
            WHERE ""walletId"" = {buyerWallet.WalletID}");
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Wallet""
            SET ""balance"" = ""balance"" + {sellerAmount}
            WHERE ""walletId"" = {sellerWallet.WalletID}");

        dispute.Status = "Resolved";
        dispute.AssignTime = dispute.AssignTime ?? DateTime.Now;
        dispute.Transaction.TransactionStatus = finalStatus;
        if (dispute.Transaction.Product != null && (dispute.Transaction.Product.Stock ?? 0) <= 0)
            dispute.Transaction.Product.Status = "Sold";
        await ArchiveOrderMessagesAsync(dispute.Transaction.TransactionID);

        _db.ArbitrationResults.Add(new ArbitrationResult
        {
            DisputeTicketID = id,
            Decision = request.Decision,
            RefundAmount = request.RefundAmount,
            CreateTime = DateTime.Now,
            WalletID = buyerWallet.WalletID > 0 ? buyerWallet.WalletID : null
        });

        await CreateNotificationAsync(buyerId, "纠纷处理完成", $"订单 {dispute.TransactionID} 退款 ¥{request.RefundAmount}", dispute.Transaction.TransactionID);
        await CreateNotificationAsync(sellerId, "纠纷处理完成", $"订单 {dispute.TransactionID} 结算 ¥{sellerAmount}", dispute.Transaction.TransactionID);
        await ApplyDisputeCreditImpactAsync(dispute, buyerId, sellerId, amount, request.RefundAmount);
        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();
        return Ok(new { message = "纠纷已处理", status = dispute.Status });
    }

    private async Task ApplyDisputeCreditImpactAsync(DisputeTicket dispute, int buyerId, int sellerId, decimal amount, decimal refundAmount)
    {
        if (amount <= 0)
            return;

        var orderLabel = $"订单 {dispute.TransactionID}";
        if (refundAmount >= amount)
        {
            await _creditService.AddCreditAsync(sellerId, -20, $"纠纷仲裁：{orderLabel} 全额退款，卖家违约");
            return;
        }

        if (refundAmount <= 0)
        {
            await _creditService.AddCreditAsync(buyerId, -10, $"纠纷仲裁：{orderLabel} 不予退款，买家责任");
            return;
        }

        await _creditService.AddCreditAsync(sellerId, -10, $"纠纷仲裁：{orderLabel} 部分退款，卖家部分责任");
        await _creditService.AddCreditAsync(buyerId, -5, $"纠纷仲裁：{orderLabel} 部分退款，买家部分责任");
    }

    private async Task<int?> PickArbitratorAsync()
    {
        var candidates = await _db.UserRoles
            .Include(ur => ur.Role)
            .ThenInclude(r => r!.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .Where(ur => ur.Role != null &&
                (ur.Role.RoleName == "Admin" ||
                 ur.Role.RoleName == "Manager" ||
                 ur.Role.RoleName == "Moderator" ||
                 ur.Role.RolePermissions.Any(rp =>
                     rp.Permission != null &&
                     (rp.Permission.PermissionName == "dashboard.view" ||
                      rp.Permission.PermissionName == "products.edit"))))
            .Select(ur => ur.UserID)
            .Distinct()
            .ToListAsync();

        if (candidates.Count == 0)
            return null;

        var openCounts = await _db.DisputeTickets
            .Where(d => d.Status == "Open" && d.ArbitratorID.HasValue)
            .GroupBy(d => d.ArbitratorID!.Value)
            .Select(g => new { ArbitratorID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ArbitratorID, x => x.Count);

        return candidates
            .OrderBy(id => openCounts.TryGetValue(id, out var count) ? count : 0)
            .ThenBy(id => id)
            .First();
    }

    private bool HasAnyPermission(params string[] permissions)
    {
        var userPermissions = User.Claims
            .Where(c => c.Type == "Permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return permissions.Any(userPermissions.Contains);
    }

    private async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserID == userId);
        if (wallet != null)
            return wallet;

        wallet = new Wallet
        {
            UserID = userId,
            Balance = 0
        };
        _db.Wallets.Add(wallet);
        return wallet;
    }

    private async Task CreateNotificationAsync(int userId, string title, string content, int transactionId)
    {
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Title = title,
            Content = content,
            TransactionID = transactionId,
            CreateTime = DateTime.Now
        });
        await Task.CompletedTask;
    }

    private async Task ArchiveOrderMessagesAsync(int transactionId)
    {
        var messages = await _db.OrderMessages
            .Where(m => m.TransactionID == transactionId && m.IsArchived != "1")
            .ToListAsync();

        foreach (var message in messages)
            message.IsArchived = "1";
    }

    private static DisputeTicketResponse MapDispute(DisputeTicket dispute)
    {
        var result = dispute.ArbitrationResults
            .OrderByDescending(r => r.CreateTime)
            .FirstOrDefault();

        return new DisputeTicketResponse
        {
            TicketID = dispute.TicketID,
            Reason = dispute.Reason ?? "",
            Status = dispute.Status ?? "",
            CreateTime = dispute.CreateTime,
            AssignTime = dispute.AssignTime,
            TransactionID = dispute.TransactionID,
            TransactionAmount = dispute.Transaction?.TransactionAmount ?? 0,
            ProductTitle = dispute.Transaction?.Product?.Title ?? "",
            BuyerName = dispute.Transaction?.User?.Username ?? "",
            SellerName = dispute.Transaction?.Product?.User?.Username ?? "",
            UserID = dispute.UserID,
            Username = dispute.User?.Username ?? "",
            ArbitratorID = dispute.ArbitratorID,
            ArbitratorName = dispute.Arbitrator?.Username ?? "",
            Decision = result?.Decision ?? "",
            RefundAmount = result?.RefundAmount,
            ResolvedTime = result?.CreateTime
        };
    }
}

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _db;

    public FriendsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<FriendResponse>>> GetFriends()
    {
        var userId = CurrentUserId();
        var friendships = await _db.FriendShips
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => (f.UserID == userId || f.FriendID == userId) && f.Status == "Accepted")
            .OrderByDescending(f => f.UpdateTime)
            .ToListAsync();

        return Ok(friendships.Select(f => MapFriend(f, userId)).ToList());
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<FriendResponse>>> GetRequests()
    {
        var userId = CurrentUserId();
        var requests = await _db.FriendShips
            .Include(f => f.User)
            .Where(f => f.FriendID == userId && f.Status == "Pending")
            .OrderByDescending(f => f.CreateTime)
            .ToListAsync();

        return Ok(requests.Select(f => MapFriend(f, userId)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<FriendResponse>> CreateRequest([FromBody] CreateFriendRequest request)
    {
        var userId = CurrentUserId();
        var target = request.UserID.HasValue
            ? await _db.Users.FindAsync(request.UserID.Value)
            : await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (target == null)
            return NotFound(new { message = "用户不存在" });
        if (target.UserID == userId)
            return BadRequest(new { message = "不能添加自己为好友" });

        var existing = await _db.FriendShips.FirstOrDefaultAsync(f =>
            (f.UserID == userId && f.FriendID == target.UserID) ||
            (f.UserID == target.UserID && f.FriendID == userId));

        if (existing != null)
        {
            if (existing.Status == "Rejected")
            {
                existing.UserID = userId;
                existing.FriendID = target.UserID;
                existing.Status = "Pending";
                existing.UpdateTime = DateTime.Now;
                await CreateNotificationAsync(target.UserID, "新的好友申请", "有人请求添加你为好友");
                await _db.SaveChangesAsync();
                existing.User = await _db.Users.FindAsync(existing.UserID);
                existing.Friend = await _db.Users.FindAsync(existing.FriendID);
                return Ok(MapFriend(existing, userId));
            }

            return BadRequest(new { message = "好友关系或申请已存在" });
        }

        var friendship = new FriendShip
        {
            UserID = userId,
            FriendID = target.UserID,
            Status = "Pending",
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now
        };

        _db.FriendShips.Add(friendship);
        await CreateNotificationAsync(target.UserID, "新的好友申请", "有人请求添加你为好友");
        await _db.SaveChangesAsync();

        friendship.User = await _db.Users.FindAsync(userId);
        friendship.Friend = target;
        return Ok(MapFriend(friendship, userId));
    }

    [HttpPost("{id}/accept")]
    public async Task<ActionResult> Accept(int id)
    {
        var friendship = await _db.FriendShips.FindAsync(id);
        if (friendship == null)
            return NotFound();
        if (friendship.FriendID != CurrentUserId())
            return Forbid();

        friendship.Status = "Accepted";
        friendship.UpdateTime = DateTime.Now;
        if (friendship.UserID.HasValue)
            await CreateNotificationAsync(friendship.UserID.Value, "好友申请已通过", "你的好友申请已被接受");
        await _db.SaveChangesAsync();
        return Ok(new { message = "已接受好友申请" });
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult> Reject(int id)
    {
        var friendship = await _db.FriendShips.FindAsync(id);
        if (friendship == null)
            return NotFound();
        if (friendship.FriendID != CurrentUserId())
            return Forbid();

        friendship.Status = "Rejected";
        friendship.UpdateTime = DateTime.Now;
        if (friendship.UserID.HasValue)
            await CreateNotificationAsync(friendship.UserID.Value, "好友申请已拒绝", "你的好友申请已被拒绝");
        await _db.SaveChangesAsync();
        return Ok(new { message = "已拒绝好友申请" });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var userId = CurrentUserId();
        var friendship = await _db.FriendShips.FindAsync(id);
        if (friendship == null)
            return NotFound();
        if (friendship.UserID != userId && friendship.FriendID != userId)
            return Forbid();

        var notifyUserId = friendship.UserID == userId ? friendship.FriendID : friendship.UserID;
        _db.FriendShips.Remove(friendship);
        if (notifyUserId.HasValue)
            await CreateNotificationAsync(notifyUserId.Value, "好友关系已解除", "有用户与你解除了好友关系");
        await _db.SaveChangesAsync();
        return Ok(new { message = "好友关系已删除" });
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private async Task CreateNotificationAsync(int userId, string title, string content)
    {
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Title = title,
            Content = content,
            CreateTime = DateTime.Now
        });
        await Task.CompletedTask;
    }

    private static FriendResponse MapFriend(FriendShip friendship, int currentUserId)
    {
        var otherUser = friendship.UserID == currentUserId ? friendship.Friend : friendship.User;
        return new FriendResponse
        {
            FriendshipID = friendship.FriendshipID,
            UserID = otherUser?.UserID ?? 0,
            Username = otherUser?.Username ?? "",
            Email = otherUser?.Email ?? "",
            Status = friendship.Status ?? "",
            CreateTime = friendship.CreateTime,
            UpdateTime = friendship.UpdateTime
        };
    }
}

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MessagesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<PrivateMessageResponse>>> GetMessages([FromQuery] int? userId)
    {
        var currentUserId = CurrentUserId();
        var query = _db.PrivateMessages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => m.SenderID == currentUserId || m.ReceiverID == currentUserId);

        if (userId.HasValue)
        {
            query = query.Where(m =>
                (m.SenderID == currentUserId && m.ReceiverID == userId.Value) ||
                (m.SenderID == userId.Value && m.ReceiverID == currentUserId));
        }

        var messages = await query.OrderByDescending(m => m.SendTime).Take(100).ToListAsync();
        return Ok(messages.Select(MapMessage).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PrivateMessageResponse>> SendMessage([FromBody] SendPrivateMessageRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUserId = CurrentUserId();
        if (request.ReceiverID == currentUserId)
            return BadRequest(new { message = "不能给自己发送私信" });

        var receiver = await _db.Users.FindAsync(request.ReceiverID);
        if (receiver == null)
            return NotFound(new { message = "接收者不存在" });

        var areFriends = await _db.FriendShips.AnyAsync(f =>
            f.Status == "Accepted" &&
            ((f.UserID == currentUserId && f.FriendID == request.ReceiverID) ||
             (f.UserID == request.ReceiverID && f.FriendID == currentUserId)));
        if (!areFriends)
            return BadRequest(new { message = "只能给好友发送私信" });

        var message = new PrivateMessage
        {
            SenderID = currentUserId,
            ReceiverID = request.ReceiverID,
            Content = request.Content,
            SendTime = DateTime.Now,
            IsRead = "0"
        };

        _db.PrivateMessages.Add(message);
        await CreateNotificationAsync(receiver.UserID, "新的私信", "你收到了一条新的私信");
        await _db.SaveChangesAsync();

        message.Sender = await _db.Users.FindAsync(currentUserId);
        message.Receiver = receiver;
        return Ok(MapMessage(message));
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult> MarkRead(int id)
    {
        var currentUserId = CurrentUserId();
        var message = await _db.PrivateMessages.FindAsync(id);
        if (message == null)
            return NotFound();
        if (message.ReceiverID != currentUserId)
            return Forbid();

        message.IsRead = "1";
        await _db.SaveChangesAsync();
        return Ok(new { message = "已读" });
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllRead()
    {
        var currentUserId = CurrentUserId();
        var messages = await _db.PrivateMessages
            .Where(m => m.ReceiverID == currentUserId && m.IsRead != "1")
            .ToListAsync();

        foreach (var message in messages)
            message.IsRead = "1";

        await _db.SaveChangesAsync();
        return Ok(new { message = "全部已读" });
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult> GetUnreadCount()
    {
        var currentUserId = CurrentUserId();
        var count = await _db.PrivateMessages.CountAsync(m => m.ReceiverID == currentUserId && m.IsRead != "1");
        return Ok(new { count });
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private async Task CreateNotificationAsync(int userId, string title, string content)
    {
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Title = title,
            Content = content,
            CreateTime = DateTime.Now
        });
        await Task.CompletedTask;
    }

    private static PrivateMessageResponse MapMessage(PrivateMessage message)
    {
        return new PrivateMessageResponse
        {
            MessageID = message.MessageID,
            Content = message.Content ?? "",
            SendTime = message.SendTime,
            IsRead = message.IsRead == "1",
            SenderID = message.SenderID,
            SenderName = message.Sender?.Username ?? "",
            ReceiverID = message.ReceiverID,
            ReceiverName = message.Receiver?.Username ?? ""
        };
    }
}

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public NotificationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<NotificationResponse>>> GetNotifications()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var notifications = await _db.Notifications
            .Where(n => n.UserID == userId)
            .OrderByDescending(n => n.CreateTime)
            .Take(100)
            .ToListAsync();

        return Ok(notifications.Select(n => new NotificationResponse
        {
            NotificationID = n.NotificationID,
            Title = n.Title ?? "",
            Content = n.Content ?? "",
            CreateTime = n.CreateTime,
            TransactionID = n.TransactionID,
            UserID = n.UserID
        }).ToList());
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteNotification(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationID == id && n.UserID == userId);
        if (notification == null)
            return NotFound();

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
        return Ok(new { message = "通知已删除" });
    }
}

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;

    public ReportsController(AppDbContext db, ICreditService creditService)
    {
        _db = db;
        _creditService = creditService;
    }

    [HttpGet]
    [RequirePermission("dashboard.view", "posts.moderate", "products.edit")]
    public async Task<ActionResult<List<ReportTicketResponse>>> GetReports([FromQuery] string? status)
    {
        var query = _db.ReportTickets.Include(r => r.Reporter).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        var reports = await query.OrderByDescending(r => r.CreateTime).Take(100).ToListAsync();
        return Ok(reports.Select(MapReport).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ReportTicketResponse>> CreateReport([FromBody] CreateReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var targetType = NormalizeTargetType(request.TargetType);
        if (targetType == null)
            return BadRequest(new { message = "举报对象类型无效" });

        var targetExists = await TargetExistsAsync(targetType, request.TargetID);
        if (!targetExists)
            return NotFound(new { message = "举报对象不存在" });

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var report = new ReportTicket
        {
            TargetType = targetType,
            TargetID = request.TargetID,
            Reason = request.Reason,
            Status = "Pending",
            CreateTime = DateTime.Now,
            ReporterID = userId
        };

        _db.ReportTickets.Add(report);
        await _db.SaveChangesAsync();

        report.Reporter = await _db.Users.FindAsync(userId);
        return Ok(MapReport(report));
    }

    [HttpPost("{id}/review")]
    [RequirePermission("dashboard.view", "posts.moderate", "products.edit")]
    public async Task<ActionResult> ReviewReport(int id, [FromBody] ReviewReportRequest request)
    {
        var report = await _db.ReportTickets.FindAsync(id);
        if (report == null)
            return NotFound();
        if (report.Status != "Pending")
            return BadRequest(new { message = "该举报已处理" });

        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "approve" and not "reject")
            return BadRequest(new { message = "审核动作只能是 approve 或 reject" });

        report.Status = action == "approve" ? "Approved" : "Rejected";
        report.Result = request.Result;
        report.ReviewTime = DateTime.Now;
        report.ReviewerID = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        if (action == "approve")
            await ApplyReportActionAsync(report);

        if (report.ReporterID.HasValue)
        {
            _db.Notifications.Add(new Notification
            {
                UserID = report.ReporterID.Value,
                Title = "举报处理结果",
                Content = $"你的举报已处理：{report.Status}",
                CreateTime = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "举报已处理", status = report.Status });
    }

    private async Task<bool> TargetExistsAsync(string targetType, int targetId)
    {
        return targetType switch
        {
            "Post" => await _db.Posts.AnyAsync(p => p.PostID == targetId),
            "Comment" => await _db.PostComments.AnyAsync(c => c.CommentID == targetId),
            "Product" => await _db.Products.AnyAsync(p => p.ProductID == targetId),
            _ => false
        };
    }

    private async Task ApplyReportActionAsync(ReportTicket report)
    {
        if (!report.TargetID.HasValue) return;

        if (report.TargetType == "Post")
        {
            var post = await _db.Posts.FindAsync(report.TargetID.Value);
            if (post != null)
            {
                post.Status = "Banned";
                if (post.UserID.HasValue)
                    await PenalizeReportedUserAsync(post.UserID.Value, -20, $"举报成立：帖子《{post.Title}》被封禁");
            }
        }
        else if (report.TargetType == "Comment")
        {
            var comment = await _db.PostComments.FindAsync(report.TargetID.Value);
            if (comment != null)
            {
                comment.Status = "Banned";
                if (comment.UserID.HasValue)
                    await PenalizeReportedUserAsync(comment.UserID.Value, -10, "举报成立：评论被封禁");
            }
        }
        else if (report.TargetType == "Product")
        {
            var product = await _db.Products.FindAsync(report.TargetID.Value);
            if (product != null)
            {
                product.Status = "Inactive";
                if (product.UserID.HasValue)
                    await PenalizeReportedUserAsync(product.UserID.Value, -20, $"举报成立：商品《{product.Title}》被下架");
            }
        }
    }

    private async Task PenalizeReportedUserAsync(int userId, int points, string reason)
    {
        await _creditService.AddCreditAsync(userId, points, reason);
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Title = "违规处理通知",
            Content = $"{reason}，信用分 {points}",
            CreateTime = DateTime.Now
        });
    }

    private static string? NormalizeTargetType(string targetType)
    {
        return targetType.Trim().ToLowerInvariant() switch
        {
            "post" => "Post",
            "comment" => "Comment",
            "product" => "Product",
            _ => null
        };
    }

    private static ReportTicketResponse MapReport(ReportTicket report)
    {
        return new ReportTicketResponse
        {
            ReportID = report.ReportID,
            TargetType = report.TargetType ?? "",
            TargetID = report.TargetID,
            Reason = report.Reason ?? "",
            Status = report.Status ?? "",
            CreateTime = report.CreateTime,
            ReviewTime = report.ReviewTime,
            Result = report.Result ?? "",
            ReporterID = report.ReporterID,
            ReporterName = report.Reporter?.Username ?? "",
            ReviewerID = report.ReviewerID
        };
    }
}

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            await _db.Database.CanConnectAsync();
            return Ok(new { status = "healthy", database = "connected" });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }
}
