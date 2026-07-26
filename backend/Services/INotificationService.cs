using Backend.Models;

namespace Backend.Services;

public class CreateNotificationOptions
{
    public int UserID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "System";
    public string? TargetType { get; set; }
    public int? TargetID { get; set; }
    public string? Link { get; set; }
    public int? TransactionID { get; set; }
    public string? EventKey { get; set; }
}

public interface INotificationService
{
    Task<Notification?> CreateAsync(CreateNotificationOptions options, bool preventDuplicate = true);
    Task<int> CreateManyAsync(IEnumerable<CreateNotificationOptions> options, bool preventDuplicate = true);
}
