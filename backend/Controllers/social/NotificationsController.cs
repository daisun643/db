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
