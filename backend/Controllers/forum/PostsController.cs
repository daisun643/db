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

        var forumExists = await _db.Forums.CountAsync(f => f.ForumID == request.ForumID && f.Status == "Active") > 0;
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
            await _db.ForumManagers.CountAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId) > 0;

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
            await _db.ForumManagers.CountAsync(fm => fm.ForumID == post.ForumID && fm.UserID == userId.Value) > 0;
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

        var exists = await _db.PostLikes.CountAsync(l => l.PostID == id && l.UserID == userId) > 0;
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

        return Ok(BuildCommentTree(comments, canViewRestrictedComments, userId));
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
            await _db.ForumManagers.CountAsync(fm =>
                comment.Post != null &&
                fm.ForumID == comment.Post.ForumID &&
                fm.UserID == userId) > 0;

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

    private static List<CommentResponse> BuildCommentTree(List<PostComment> comments, bool canViewRestricted, int? userId)
    {
        var nodes = comments.ToDictionary(c => c.CommentID, c => new CommentResponse
        {
            CommentID = c.CommentID,
            Content = (c.Status == "Deleted" && !canViewRestricted && c.UserID != userId) ? "" : (c.Content ?? ""),
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

        roots = roots.Where(KeepInTree).ToList();
        return roots;
    }

    private static bool KeepInTree(CommentResponse node)
    {
        node.Replies = node.Replies.Where(KeepInTree).ToList();
        return node.Status != "Deleted" || node.Replies.Count > 0;
    }
}
