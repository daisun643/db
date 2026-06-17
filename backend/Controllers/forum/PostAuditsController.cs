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
