using System.ComponentModel.DataAnnotations;

namespace Backend.Models.DTOs;

public class FriendResponse
{
    public int FriendshipID { get; set; }
    public int UserID { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class CreateFriendRequest
{
    public int? UserID { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}

public class PrivateMessageResponse
{
    public int MessageID { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SendTime { get; set; }
    public bool IsRead { get; set; }
    public int SenderID { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public int ReceiverID { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
}

public class ConversationResponse
{
    public int FriendshipID { get; set; }
    public int UserID { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? LatestMessageContent { get; set; }
    public DateTime? LatestMessageTime { get; set; }
    public bool LatestMessageIsMine { get; set; }
    public int UnreadCount { get; set; }
}
public class SendPrivateMessageRequest
{
    [Range(1, int.MaxValue)]
    public int ReceiverID { get; set; }

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

public class NotificationResponse
{
    public int NotificationID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public int? TargetID { get; set; }
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadTime { get; set; }
    public DateTime? CreateTime { get; set; }
    public int? TransactionID { get; set; }
    public int? UserID { get; set; }
}

public class NotificationListResponse
{
    public List<NotificationResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class CreateSystemNotificationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public List<int>? ReceiverUserIDs { get; set; }
}

