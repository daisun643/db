using Backend.Authorization;
using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Threading.Channels;

namespace Backend.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly INotificationPushService _pushService;

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(25);

    public NotificationsController(AppDbContext db, INotificationService notificationService, INotificationPushService pushService)
    {
        _db = db;
        _notificationService = notificationService;
        _pushService = pushService;
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

    /// <summary>
    /// SSE 长连接：向当前用户实时推送通知/私信事件。
    /// 事件类型：notification（信号，前端重拉列表）、message（完整私信体）。
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var (connectionId, events) = _pushService.Subscribe(userId);

        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        // 关闭 nginx 类反向代理的响应缓冲，保证事件即时到达
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await WriteFrameAsync(": connected\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var hasData = false;
                var timedOut = false;
                try
                {
                    // 定时器兼作心跳：空闲超时写注释帧，防止代理/网络断开空闲连接
                    hasData = await events.WaitToReadAsync(cancellationToken).AsTask()
                        .WaitAsync(HeartbeatInterval, cancellationToken);
                }
                catch (TimeoutException)
                {
                    timedOut = true;
                }

                if (timedOut)
                {
                    await WriteFrameAsync(": ping\n\n", cancellationToken);
                }
                else if (!hasData)
                {
                    // 通道已关闭，连接被注销，结束推送
                    break;
                }
                else
                {
                    while (events.TryRead(out var frame))
                    {
                        await WriteFrameAsync(frame, cancellationToken);
                    }
                }

                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 客户端断开或服务器关闭，属正常退出
        }
        finally
        {
            _pushService.Unsubscribe(userId, connectionId);
        }
    }

    private async Task WriteFrameAsync(string frame, CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(frame), cancellationToken);
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
            // readTime 由 TRG_Notification_ReadTime 在 isRead 翻转为已读时自动写入
            notification.IsRead = "1";
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

        foreach (var notification in notifications)
        {
            notification.IsRead = "1";
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

    [HttpGet("announcements")]
    [AllowAnonymous]
    public async Task<ActionResult> GetAnnouncements([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var notifications = await _db.Notifications
            .Where(n => n.Type == "System" && n.TargetType == "System")
            .OrderByDescending(n => n.CreateTime)
            .ThenByDescending(n => n.NotificationID)
            .ToListAsync();

        // 按 EventKey 前缀去重（同一广播每个用户一条记录，前缀为 system:{timestamp}）
        var distinct = notifications
            .GroupBy(n => n.EventKey != null && n.EventKey.Contains(':')
                ? n.EventKey[..n.EventKey.LastIndexOf(':')]
                : n.EventKey ?? "")
            .Select(g => g.First())
            .ToList();

        var total = distinct.Count;
        var announcements = distinct
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                id = n.NotificationID,
                title = n.Title ?? "",
                content = n.Content ?? "",
                date = n.CreateTime
            })
            .ToList();

        return Ok(new { items = announcements, total, page, pageSize });
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


