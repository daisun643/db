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
using System.Text.RegularExpressions;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly IMediaStorageService _mediaStorageService;
    private readonly INotificationService _notificationService;
    private static readonly string[] SensitiveWords = ["违禁", "敏感词", "spam"];

    public PostsController(AppDbContext db, ICreditService creditService, IMediaStorageService mediaStorageService, INotificationService notificationService)
    {
        _db = db;
        _creditService = creditService;
        _mediaStorageService = mediaStorageService;
        _notificationService = notificationService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<PostListItemResponse>>> GetAll(
        [FromQuery] int? forumId,
        [FromQuery] int? authorId,
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] string? sort = "latest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var normalizedSort = (sort ?? "latest").Trim().ToLowerInvariant();

        if (normalizedSort != "latest")
            return BadRequest(new { message = "排序参数不合法" });

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest(new { message = "时间范围不合法" });

        var query = _db.Posts
            .Include(p => p.User)
            .Include(p => p.Forum)
            .AsQueryable();

        if (forumId.HasValue)
            query = query.Where(p => p.ForumID == forumId.Value);

        if (authorId.HasValue)
            query = query.Where(p => p.UserID == authorId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(p =>
                (p.Title != null && p.Title.Contains(trimmed)) ||
                (p.Content != null && p.Content.Contains(trimmed)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizePostStatus(status);
            if (normalizedStatus == null)
                return BadRequest(new { message = "帖子状态不合法" });

            // 版块管理人员（版主/管理员/创建者）可在自己版块内按审核状态筛选帖子
            if (!IsPublicPostStatus(normalizedStatus) && !CanViewModerationStatus() &&
                !await CanModerateForumPostsAsync(forumId))
                return Forbid();

            query = query.Where(p => p.Status == normalizedStatus);
        }
        else
        {
            query = query.Where(p => p.Status == "Active" || p.Status == "Elite" || p.Status == "Pinned");
        }

        if (from.HasValue)
            query = query.Where(p => p.CreateTime >= from);

        if (to.HasValue)
            query = query.Where(p => p.CreateTime <= to);

        var totalCount = await query.CountAsync();
        var posts = await query
            .OrderByDescending(p => p.Status == "Pinned" ? 1 : 0)
            .ThenByDescending(p => p.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers["X-Total-Count"] = totalCount.ToString();
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

        var forumExists = await _db.Forums.CountAsync(f => f.ForumID == request.ForumID && f.Status == "Active") > 0;
        if (!forumExists)
            return BadRequest(new { message = "论坛不存在或不可用" });

        var hitWord = FindSensitiveWord(request.Title, request.Content);
        var normalizedImageUrls = NormalizeImageUrls(request.ImageUrls);
        var post = new Post
        {
            ForumID = request.ForumID,
            Title = request.Title.Trim(),
            Content = request.Content,
            UserID = userId,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
            Status = hitWord == null ? "Active" : "PendingReview",
            LikeCount = 0,
            ViewCount = 0
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        await ReplacePostMediaAsync(post.PostID, userId, normalizedImageUrls);

        await CreateMentionNotificationsAsync(userId, request.Content, "帖子提及", $"在帖子《{post.Title}》中提到了你", "Post", post.PostID, $"/forums");
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
        var normalizedImageUrls = NormalizeImageUrls(request.ImageUrls);
        post.Title = request.Title.Trim();
        post.Content = request.Content;
        post.UpdateTime = DateTime.Now;
        post.Status = hitWord == null ? post.Status : "PendingReview";
        await ReplacePostMediaAsync(post.PostID, userId, normalizedImageUrls);

        await _db.SaveChangesAsync();
        await CreateMentionNotificationsAsync(userId, request.Content, "帖子提及", $"在帖子《{post.Title}》中提到了你", "Post", post.PostID, $"/forums");
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
            await _db.ForumManagers.CountAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId) > 0 ||
            await IsForumCreatorAsync(post.ForumID, userId);

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
            await _db.ForumManagers.CountAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId) > 0 ||
            await IsForumCreatorAsync(post.ForumID, userId) ||
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

    /// <summary>
    /// 版块创建者是默认版主，对该版块帖子拥有与版主一致的管理权限。
    /// </summary>
    private async Task<bool> IsForumCreatorAsync(int? forumId, int userId)
    {
        if (!forumId.HasValue)
            return false;

        return await _db.Forums.CountAsync(f =>
            f.ForumID == forumId.Value && f.CreatorID == userId) > 0;
    }

    /// <summary>
    /// 当前用户是否是指定版块的管理人员（版主/管理员/创建者），
    /// 用于允许版块管理人员按待审核、已封禁等状态筛选本版块帖子。
    /// </summary>
    private async Task<bool> CanModerateForumPostsAsync(int? forumId)
    {
        if (!forumId.HasValue)
            return false;

        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return false;

        return await _db.ForumManagers.CountAsync(fm =>
                fm.ForumID == forumId.Value && fm.UserID == userId.Value) > 0 ||
            await IsForumCreatorAsync(forumId.Value, userId.Value);
    }

    private async Task<bool> CanViewRestrictedPostAsync(Post post)
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return false;

        return post.UserID == userId.Value ||
            CanViewModerationStatus() ||
            await _db.ForumManagers.CountAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId.Value) > 0 ||
            await IsForumCreatorAsync(post.ForumID, userId.Value);
    }

    private static bool IsPublicPostStatus(string? status)
    {
        return status is "Active" or "Elite" or "Pinned";
    }

    private static string? NormalizePostStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "active" => "Active",
            "elite" => "Elite",
            "pinned" => "Pinned",
            "banned" => "Banned",
            "pendingreview" => "PendingReview",
            "pending-review" => "PendingReview",
            "deleted" => "Deleted",
            _ => null
        };
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

        var exists = await _db.PostLikes.CountAsync(l => l.PostID == id && l.UserID == userId) > 0;
        if (!exists)
        {
            _db.PostLikes.Add(new PostLike { PostID = id, UserID = userId, CreateTime = DateTime.Now });
            post.LikeCount = (post.LikeCount ?? 0) + 1;
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
            await _db.SaveChangesAsync();
        }

        return Ok(new { liked = false, likeCount = post.LikeCount ?? 0 });
    }

    [HttpDelete("{id}/favorite")]
    [Authorize]
    public async Task<ActionResult> Unfavorite(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();

        var folderPosts = await _db.FolderPosts
            .Include(fp => fp.Folder)
            .Where(fp => fp.PostID == id && fp.Folder != null && fp.Folder.UserID == userId)
            .ToListAsync();
        if (folderPosts.Count > 0)
        {
            _db.FolderPosts.RemoveRange(folderPosts);
            await _db.SaveChangesAsync();
        }

        return Ok(new { favorited = false });
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
            (userId.HasValue && await _db.ForumManagers.CountAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId.Value) > 0);

        var comments = await _db.PostComments
            .Include(c => c.User)
            .Where(c => c.PostID == postId &&
                c.Status != "Banned" &&
                (c.Status == "Active" || c.Status == "Deleted" || (userId.HasValue && c.UserID == userId.Value) || canViewRestrictedComments))
            .OrderBy(c => c.CreateTime)
            .ToListAsync();

        return Ok(BuildCommentTree(comments, canViewRestrictedComments, userId,
            await GetUserAvatarUrlsAsync(comments
                .Where(c => c.UserID.HasValue)
                .Select(c => c.UserID!.Value))));
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
            var parentExists = await _db.PostComments.CountAsync(c =>
                c.CommentID == request.ParentCommentID.Value &&
                c.PostID == postId &&
                c.Status != "Deleted") > 0;
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

        await CreateMentionNotificationsAsync(userId, request.Content, "评论提及", $"在帖子《{post.Title}》的评论中提到了你", "Post", post.PostID, $"/forums");

        // 评论回复通知：两个独立判断
        // 1. 只要评论者不是帖子作者 → 通知帖子作者
        if (post.UserID.HasValue && post.UserID.Value != userId)
        {
            await _notificationService.CreateAsync(new CreateNotificationOptions
            {
                UserID = post.UserID.Value,
                Type = "Reply",
                Title = "帖子新评论",
                Content = $"有人在帖子《{post.Title}》中发表了评论",
                TargetType = "Post",
                TargetID = post.PostID,
                Link = $"/forums",
                EventKey = $"reply:post:{comment.CommentID}:{post.UserID.Value}"
            });
        }

        // 2. 如果回复了别人的评论，且回复者不是父评论作者 → 通知父评论作者
        if (request.ParentCommentID.HasValue)
        {
            var parentComment = await _db.PostComments.FindAsync(request.ParentCommentID.Value);
            if (parentComment != null && parentComment.UserID.HasValue
                && parentComment.UserID.Value != userId
                && parentComment.UserID.Value != post.UserID)  // 避免父评论作者=帖子作者时重复通知
            {
                await _notificationService.CreateAsync(new CreateNotificationOptions
                {
                    UserID = parentComment.UserID.Value,
                    Type = "Reply",
                    Title = "评论回复",
                    Content = $"有人在帖子《{post.Title}》中回复了你的评论",
                    TargetType = "Comment",
                    TargetID = parentComment.CommentID,
                    Link = $"/forums",
                    EventKey = $"reply:comment:{comment.CommentID}:{parentComment.UserID.Value}"
                });
            }
        }

        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        var commenterAvatar = await GetUserAvatarUrlsAsync(new[] { userId });
        return Ok(new CommentResponse
        {
            CommentID = comment.CommentID,
            Content = comment.Content ?? "",
            Status = comment.Status ?? "",
            CreateTime = comment.CreateTime,
            UserID = comment.UserID,
            Username = user?.Username ?? "",
            AvatarUrl = commenterAvatar.TryGetValue(userId, out var avatar) ? avatar : "",
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
            await _db.ForumManagers.CountAsync(fm =>
                comment.Post != null &&
                fm.ForumID == comment.Post.ForumID &&
                fm.UserID == userId) > 0;

        if (!canDelete)
            return Forbid();

        comment.Status = "Deleted";
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

    [HttpGet("me/comments")]
    [Authorize]
    public async Task<ActionResult<List<MyCommentResponse>>> GetMyComments()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var comments = await _db.PostComments
            .Include(c => c.Post)
            .ThenInclude(p => p!.Forum)
            .Where(c => c.UserID == userId && c.Status != "Deleted" && c.Post != null && c.Post.Status != "Deleted")
            .OrderByDescending(c => c.CreateTime)
            .ToListAsync();

        return Ok(comments.Select(c => new MyCommentResponse
        {
            CommentID = c.CommentID,
            Content = c.Content ?? "",
            Status = c.Status ?? "",
            CreateTime = c.CreateTime,
            ParentCommentID = c.ParentCommentID,
            PostID = c.PostID,
            PostTitle = c.Post!.Title ?? "",
            ForumID = c.Post.ForumID,
            ForumName = c.Post.Forum?.ForumName ?? ""
        }).ToList());
    }

    private async Task<List<PostListItemResponse>> MapPostListAsync(IEnumerable<Post> posts)
    {
        var postList = posts.ToList();
        var postIds = postList.Select(p => p.PostID).ToList();
        var mediaRows = await _db.PostMedia
            .Include(x => x.Media)
            .Where(x => postIds.Contains(x.PostID))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.MediaID)
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

        var mediaByPost = mediaRows
            .GroupBy(x => x.PostID)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Media?.Url).Where(HasUrl).Select(url => url!).ToList());

        var avatarByUser = await GetUserAvatarUrlsAsync(postList
            .Where(p => p.UserID.HasValue)
            .Select(p => p.UserID!.Value));

        return postList.Select(p => new PostListItemResponse
        {
            PostID = p.PostID,
            Title = p.Title ?? "",
            ContentPreview = BuildPreview(p.Content),
            LikeCount = p.LikeCount ?? 0,
            ViewCount = p.ViewCount ?? 0,
            CommentCount = commentCounts.TryGetValue(p.PostID, out var count) ? count : 0,
            Status = p.Status ?? "",
            CreateTime = p.CreateTime,
            UpdateTime = p.UpdateTime,
            UserID = p.UserID,
            Username = p.User?.Username ?? "",
            AvatarUrl = p.UserID.HasValue && avatarByUser.TryGetValue(p.UserID.Value, out var authorAvatar) ? authorAvatar : "",
            ForumID = p.ForumID,
            ForumName = p.Forum?.ForumName ?? "",
            ImageUrls = ResolveImageUrls(mediaByPost, p),
            IsLiked = likedPostIds.Contains(p.PostID),
            IsFavorited = favoritedPostIds.Contains(p.PostID)
        }).ToList();
    }

    private static List<string> ResolveImageUrls(Dictionary<int, List<string>> mediaByPost, Post post)
    {
        if (post.PostID != 0 &&
            mediaByPost.TryGetValue(post.PostID, out var mediaImageUrls) &&
            mediaImageUrls.Count > 0)
        {
            return mediaImageUrls.Take(6).ToList();
        }

        return new List<string>();
    }

    private async Task<PostDetailResponse> MapPostDetailAsync(Post post, bool incrementView = false)
    {
        if (incrementView)
        {
            post.ViewCount = (post.ViewCount ?? 0) + 1;
            await _db.SaveChangesAsync();
        }

        var listItem = (await MapPostListAsync(new[] { post })).Single();
        return new PostDetailResponse
        {
            PostID = listItem.PostID,
            Title = listItem.Title,
            ContentPreview = listItem.ContentPreview,
            Content = post.Content ?? "",
            LikeCount = listItem.LikeCount,
            ViewCount = listItem.ViewCount,
            CommentCount = listItem.CommentCount,
            Status = listItem.Status,
            CreateTime = listItem.CreateTime,
            UpdateTime = listItem.UpdateTime,
            UserID = listItem.UserID,
            Username = listItem.Username,
            AvatarUrl = listItem.AvatarUrl,
            ForumID = listItem.ForumID,
            ForumName = listItem.ForumName,
            ImageUrls = listItem.ImageUrls,
            IsLiked = listItem.IsLiked,
            IsFavorited = listItem.IsFavorited
        };
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

    private static List<string> NormalizeImageUrls(IEnumerable<string>? urls)
    {
        if (urls == null)
            return new List<string>();

        return urls
            .Select(url => url?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Where(IsValidImageUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }

    private static bool IsValidImageUrl(string url)
    {
        if (url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    private static bool HasUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url);
    }

    private async Task ReplacePostMediaAsync(int postId, int? uploadedByUserId, IEnumerable<string> imageUrls)
    {
        var normalized = NormalizeImageUrls(imageUrls);

        var existingLinks = await _db.PostMedia
            .Include(x => x.Media)
            .Where(x => x.PostID == postId)
            .ToListAsync();
        var existingLookup = existingLinks
            .Where(x => !string.IsNullOrWhiteSpace(x.Media?.Url))
            .ToDictionary(x => x.Media!.Url!, x => x.Media!, StringComparer.OrdinalIgnoreCase);

        var incomingSet = normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var removed in existingLinks.Where(x => !incomingSet.Contains(x.Media?.Url ?? "")))
        {
            await _mediaStorageService.DeleteByUrlAsync(removed.Media?.Url);
            if (removed.Media != null)
                _db.MediaFiles.Remove(removed.Media);
        }
        _db.PostMedia.RemoveRange(existingLinks);

        for (var i = 0; i < normalized.Count; i++)
        {
            var url = normalized[i];
            if (!existingLookup.TryGetValue(url, out var media))
            {
                media = new MediaFile
                {
                    StorageProvider = url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ? "s3" : "external",
                    Url = url,
                    UploadedByUserID = uploadedByUserId,
                    UploadTime = DateTime.UtcNow
                };
                _db.MediaFiles.Add(media);
            }
            _db.PostMedia.Add(new PostMedia { PostID = postId, Media = media, DisplayOrder = i });
        }
    }

    private async Task CreateMentionNotificationsAsync(int senderId, string content, string title, string notificationContent, string targetType, int targetId, string link)
    {
        var mentionTokens = Regex.Matches(content ?? string.Empty, @"@([\p{L}\p{N}_\-.]{1,80})")
            .Select(match => match.Groups[1].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (mentionTokens.Count == 0)
            return;

        var lowerTokens = mentionTokens.Select(token => token.ToLowerInvariant()).ToHashSet();
        var candidateUsers = await _db.Users
            .Where(u => u.UserID != senderId)
            .ToListAsync();
        var mentionedUsers = candidateUsers
            .Where(u => MatchesMentionToken(u, lowerTokens))
            .ToList();

        foreach (var user in mentionedUsers)
        {
            await _notificationService.CreateAsync(new CreateNotificationOptions
            {
                UserID = user.UserID,
                Type = "Mention",
                Title = title,
                Content = notificationContent,
                TargetType = targetType,
                TargetID = targetId,
                Link = link,
                EventKey = $"mention:{targetType}:{targetId}:{user.UserID}:{title}"
            });
        }
    }

    private static bool MatchesMentionToken(User user, HashSet<string> lowerTokens)
    {
        var candidates = new List<string?>
        {
            user.Username,
            user.UserCode,
            user.Email
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var atIndex = user.Email.IndexOf("@", StringComparison.Ordinal);
            if (atIndex > 0)
                candidates.Add(user.Email[..atIndex]);
        }

        return candidates
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant())
            .Any(lowerTokens.Contains);
    }
    /// <summary>
    /// 批量查询用户头像：一次 IN 查询，返回 userId → 头像 URL 映射，避免 N+1。
    /// </summary>
    private async Task<Dictionary<int, string>> GetUserAvatarUrlsAsync(IEnumerable<int> userIds)
    {
        var idList = userIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<int, string>();

        return await _db.UserAvatars
            .Include(x => x.Media)
            .Where(x => idList.Contains(x.UserID) && x.Media != null && x.Media.Url != null && x.Media.Url != "")
            .ToDictionaryAsync(x => x.UserID, x => x.Media!.Url!);
    }

    private static List<CommentResponse> BuildCommentTree(List<PostComment> comments, bool canViewRestricted, int? userId, Dictionary<int, string> avatarByUser)
    {
        var nodes = comments.ToDictionary(c => c.CommentID, c => new CommentResponse
        {
            CommentID = c.CommentID,
            Content = (c.Status == "Deleted" && !canViewRestricted && c.UserID != userId) ? "" : (c.Content ?? ""),
            Status = c.Status ?? "",
            CreateTime = c.CreateTime,
            UserID = c.UserID,
            Username = c.User?.Username ?? "",
            AvatarUrl = c.UserID.HasValue && avatarByUser.TryGetValue(c.UserID.Value, out var commenterAvatar) ? commenterAvatar : "",
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

        roots = roots.Where(KeepInTree).ToList();
        return roots;
    }

    private static bool KeepInTree(CommentResponse node)
    {
        node.Replies = node.Replies.Where(KeepInTree).ToList();
        return node.Status != "Deleted" || node.Replies.Count > 0;
    }
}
