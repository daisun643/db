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
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;

    public NotificationsController(AppDbContext db, INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationListResponse>> GetNotifications(
        [FromQuery] string? type,
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = CurrentUserId();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Notifications.Where(n => n.UserID == userId);

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim();
            query = query.Where(n => n.Type == normalizedType);
        }

        if (isRead.HasValue)
        {
            query = isRead.Value
                ? query.Where(n => n.IsRead == "1")
                : query.Where(n => n.IsRead != "1" || n.IsRead == null);
        }

        var total = await query.CountAsync();
        var notifications = await query
            .OrderByDescending(n => n.CreateTime)
            .ThenByDescending(n => n.NotificationID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new NotificationListResponse
        {
            Items = notifications.Select(MapNotification).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult> GetUnreadCount()
    {
        var userId = CurrentUserId();
        var count = await _db.Notifications.CountAsync(n => n.UserID == userId && (n.IsRead != "1" || n.IsRead == null));
        return Ok(new { count });
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult> MarkRead(int id)
    {
        var userId = CurrentUserId();
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationID == id && n.UserID == userId);
        if (notification == null)
            return NotFound();

        if (notification.IsRead != "1")
        {
            notification.IsRead = "1";
            notification.ReadTime = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "已读" });
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllRead()
    {
        var userId = CurrentUserId();
        var notifications = await _db.Notifications
            .Where(n => n.UserID == userId && (n.IsRead != "1" || n.IsRead == null))
            .ToListAsync();

        var now = DateTime.Now;
        foreach (var notification in notifications)
        {
            notification.IsRead = "1";
            notification.ReadTime = now;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "全部已读", count = notifications.Count });
    }

    [HttpPost("system")]
    [RequirePermission("dashboard.view")]
    public async Task<ActionResult> CreateSystemNotification([FromBody] CreateSystemNotificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var receiverIds = request.ReceiverUserIDs?.Distinct().ToList();
        if (receiverIds == null || receiverIds.Count == 0)
        {
            receiverIds = await _db.Users
                .Where(u => u.Status == "Active")
                .Select(u => u.UserID)
                .ToListAsync();
        }
        else
        {
            receiverIds = await _db.Users
                .Where(u => receiverIds.Contains(u.UserID))
                .Select(u => u.UserID)
                .ToListAsync();
        }

        var eventKeyPrefix = $"system:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var created = await _notificationService.CreateManyAsync(receiverIds.Select(userId => new CreateNotificationOptions
        {
            UserID = userId,
            Type = "System",
            Title = request.Title,
            Content = request.Content,
            TargetType = "System",
            EventKey = $"{eventKeyPrefix}:{userId}"
        }));

        await _db.SaveChangesAsync();
        return Ok(new { message = "系统通知已创建", count = created });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteNotification(int id)
    {
        var userId = CurrentUserId();
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationID == id && n.UserID == userId);
        if (notification == null)
            return NotFound();

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
        return Ok(new { message = "通知已删除" });
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private static NotificationResponse MapNotification(Notification notification)
    {
        return new NotificationResponse
        {
            NotificationID = notification.NotificationID,
            Title = notification.Title ?? "",
            Content = notification.Content ?? "",
            Type = notification.Type ?? "System",
            TargetType = notification.TargetType,
            TargetID = notification.TargetID,
            Link = notification.Link,
            IsRead = notification.IsRead == "1",
            ReadTime = notification.ReadTime,
            CreateTime = notification.CreateTime,
            TransactionID = notification.TransactionID,
            UserID = notification.UserID
        };
    }
}


