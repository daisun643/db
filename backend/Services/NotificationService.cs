using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Notification?> CreateAsync(CreateNotificationOptions options, bool preventDuplicate = true)
    {
        if (options.UserID <= 0)
            return null;

        var title = (options.Title ?? string.Empty).Trim();
        var content = (options.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            return null;

        var eventKey = string.IsNullOrWhiteSpace(options.EventKey) ? null : options.EventKey.Trim();
        if (preventDuplicate && eventKey != null)
        {
            var existsInDb = await _db.Notifications.CountAsync(n => n.EventKey == eventKey) > 0;
            var existsInLocal = _db.ChangeTracker.Entries<Notification>()
                .Any(e => e.Entity.EventKey == eventKey && e.State != EntityState.Deleted);
            if (existsInDb || existsInLocal)
                return null;
        }

        var notification = new Notification
        {
            UserID = options.UserID,
            Title = title,
            Content = content,
            Type = NormalizeType(options.Type),
            TargetType = string.IsNullOrWhiteSpace(options.TargetType) ? null : options.TargetType.Trim(),
            TargetID = options.TargetID,
            Link = string.IsNullOrWhiteSpace(options.Link) ? null : options.Link.Trim(),
            TransactionID = options.TransactionID,
            EventKey = eventKey,
            IsRead = "0",
            CreateTime = DateTime.Now
        };

        _db.Notifications.Add(notification);
        return notification;
    }

    public async Task<int> CreateManyAsync(IEnumerable<CreateNotificationOptions> options, bool preventDuplicate = true)
    {
        var count = 0;
        foreach (var item in options)
        {
            if (await CreateAsync(item, preventDuplicate) != null)
                count++;
        }
        return count;
    }

    private static string NormalizeType(string? type)
    {
        var value = string.IsNullOrWhiteSpace(type) ? "System" : type.Trim();
        return value.Length > 50 ? value[..50] : value;
    }
}

