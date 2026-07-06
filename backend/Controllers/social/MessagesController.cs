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

        var latestMessages = await query.OrderByDescending(m => m.SendTime).Take(100).ToListAsync();
        return Ok(latestMessages.OrderBy(m => m.SendTime).Select(MapMessage).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PrivateMessageResponse>> SendMessage([FromBody] SendPrivateMessageRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUserId = CurrentUserId();
        if (request.ReceiverID == currentUserId)
            return BadRequest(new { message = "不能给自己发送私信" });
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "私信内容不能为空" });

        var receiver = await _db.Users.FindAsync(request.ReceiverID);
        if (receiver == null)
            return NotFound(new { message = "接收者不存在" });
        if (!string.Equals(receiver.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "接收者当前不可用" });

        var areFriends = await _db.FriendShips.CountAsync(f =>
            f.Status == "Accepted" &&
            ((f.UserID == currentUserId && f.FriendID == request.ReceiverID) ||
             (f.UserID == request.ReceiverID && f.FriendID == currentUserId))) > 0;
        if (!areFriends)
            return BadRequest(new { message = "只能给好友发送私信" });

        var message = new PrivateMessage
        {
            SenderID = currentUserId,
            ReceiverID = request.ReceiverID,
            Content = request.Content.Trim(),
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
    public async Task<ActionResult> MarkAllRead([FromQuery] int? userId)
    {
        var currentUserId = CurrentUserId();
        if (userId == currentUserId)
            return BadRequest(new { message = "不能选择自己作为会话对象" });

        var query = _db.PrivateMessages
            .Where(m => m.ReceiverID == currentUserId && m.IsRead != "1");

        if (userId.HasValue)
            query = query.Where(m => m.SenderID == userId.Value);

        var messages = await query.ToListAsync();

        foreach (var message in messages)
            message.IsRead = "1";

        await _db.SaveChangesAsync();
        return Ok(new { message = userId.HasValue ? "当前会话已读" : "全部已读", count = messages.Count });
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
            Content = message.Content,
            SendTime = message.SendTime,
            IsRead = message.IsRead == "1",
            SenderID = message.SenderID,
            SenderName = message.Sender?.Username ?? "",
            ReceiverID = message.ReceiverID,
            ReceiverName = message.Receiver?.Username ?? ""
        };
    }
}
