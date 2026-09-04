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
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly INotificationService _notificationService;

    public ReportsController(AppDbContext db, ICreditService creditService, INotificationService notificationService)
    {
        _db = db;
        _creditService = creditService;
        _notificationService = notificationService;
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

        // 举报受理通知：通知所在版块的版主（含创建者）；商品或无版主版块则通知所有管理员
        var reporter = await _db.Users.FindAsync(userId);
        var targetLabel = targetType switch
        {
            "Post" => "帖子",
            "Comment" => "评论",
            "Product" => "商品",
            _ => "内容"
        };
        var receivers = await GetReportNotifyReceiversAsync(targetType, request.TargetID);
        foreach (var receiverId in receivers.Where(id => id != userId).Distinct())
        {
            await _notificationService.CreateAsync(new CreateNotificationOptions
            {
                UserID = receiverId,
                Type = "Report",
                Title = "收到新举报",
                Content = $"{reporter?.Username ?? "有用户"} 举报了{targetLabel}，原因：{request.Reason}",
                TargetType = targetType,
                TargetID = request.TargetID,
                Link = targetType == "Post" ? $"/forums/post/{request.TargetID}" : null,
                EventKey = $"report:filed:{targetType}:{request.TargetID}:{receiverId}"
            });
        }

        await _db.SaveChangesAsync();

        report.Reporter = reporter;
        return Ok(MapReport(report));
    }

    // 举报受理通知接收人：帖子/评论 → 所在版块的版主∪创建者；商品或无版主版块 → 所有管理员
    private async Task<List<int>> GetReportNotifyReceiversAsync(string targetType, int targetId)
    {
        int? forumId = null;
        if (targetType == "Post")
        {
            forumId = (await _db.Posts.FindAsync(targetId))?.ForumID;
        }
        else if (targetType == "Comment")
        {
            var comment = await _db.PostComments
                .Include(c => c.Post)
                .FirstOrDefaultAsync(c => c.CommentID == targetId);
            forumId = comment?.Post?.ForumID;
        }

        if (forumId.HasValue)
        {
            var receivers = await _db.ForumManagers
                .Where(fm => fm.ForumID == forumId.Value)
                .Select(fm => fm.UserID)
                .ToListAsync();
            var creatorId = await _db.Forums
                .Where(f => f.ForumID == forumId.Value)
                .Select(f => f.CreatorID ?? 0)
                .FirstOrDefaultAsync();
            if (creatorId > 0)
                receivers.Add(creatorId);

            if (receivers.Count > 0)
                return receivers;
        }

        return await _db.UserRoles
            .Where(ur => ur.Role != null && ur.Role.RoleName == "Manager")
            .Select(ur => ur.UserID)
            .Distinct()
            .ToListAsync();
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
            await _notificationService.CreateAsync(new CreateNotificationOptions
            {
                UserID = report.ReporterID.Value,
                Type = "Report",
                Title = "举报处理结果",
                Content = $"你的举报已处理：{report.Status}",
                TargetType = report.TargetType,
                TargetID = report.TargetID,
                EventKey = $"report:{report.TargetType}:{report.TargetID}:review:{report.ReporterID.Value}"
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "举报已处理", status = report.Status });
    }

    private async Task<bool> TargetExistsAsync(string targetType, int targetId)
    {
        return targetType switch
        {
            "Post" => await _db.Posts.CountAsync(p => p.PostID == targetId) > 0,
            "Comment" => await _db.PostComments.CountAsync(c => c.CommentID == targetId) > 0,
            "Product" => await _db.Products.CountAsync(p => p.ProductID == targetId) > 0,
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
                    await PenalizeReportedUserAsync(post.UserID.Value, -20, $"举报成立：帖子《{post.Title}》被封禁", "Post", post.PostID);
            }
        }
        else if (report.TargetType == "Comment")
        {
            var comment = await _db.PostComments.FindAsync(report.TargetID.Value);
            if (comment != null)
            {
                comment.Status = "Banned";
                if (comment.UserID.HasValue)
                    await PenalizeReportedUserAsync(comment.UserID.Value, -10, "举报成立：评论被封禁", "Comment", comment.CommentID);
            }
        }
        else if (report.TargetType == "Product")
        {
            var product = await _db.Products.FindAsync(report.TargetID.Value);
            if (product != null)
            {
                product.Status = "Inactive";
                if (product.UserID.HasValue)
                    await PenalizeReportedUserAsync(product.UserID.Value, -20, $"举报成立：商品《{product.Title}》被下架", "Product", product.ProductID);
            }
        }
    }

    private async Task PenalizeReportedUserAsync(int userId, int points, string reason, string targetType, int targetId)
    {
        await _creditService.AddCreditAsync(userId, points, reason);
        await _notificationService.CreateAsync(new CreateNotificationOptions
        {
            UserID = userId,
            Type = "Report",
            Title = "违规处理通知",
            Content = $"{reason}，信用分 {points}",
            TargetType = targetType,
            TargetID = targetId,
            EventKey = $"report:penalty:{targetType}:{targetId}:{userId}"
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
