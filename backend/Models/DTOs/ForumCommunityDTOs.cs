using System.ComponentModel.DataAnnotations;

namespace Backend.Models.DTOs;

public class ForumSummaryResponse
{
    public int ForumID { get; set; }
    public string ForumName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreateTime { get; set; }
    public int PostCount { get; set; }
    public List<ForumManagerResponse> Managers { get; set; } = new();
}

public class CreateForumRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string ForumName { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
}

public class UpdateForumRequest : CreateForumRequest
{
    public string Status { get; set; } = "Active";
}

public class AssignForumManagerRequest
{
    [Required]
    public int UserID { get; set; }
}

public class ForumManagerResponse
{
    public int UserID { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class PostListItemResponse
{
    public int PostID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public int HeatScore { get; set; }
    public int LikeCount { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
    public int? UserID { get; set; }
    public string Username { get; set; } = string.Empty;
    public int? ForumID { get; set; }
    public string ForumName { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> ImageUrls { get; set; } = new();
    public bool IsLiked { get; set; }
    public bool IsFavorited { get; set; }
}

public class PostDetailResponse : PostListItemResponse
{
    public string Content { get; set; } = string.Empty;
}

public class CreatePostRequest
{
    [Required]
    public int ForumID { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = new();
    public List<string> TagNames { get; set; } = new();
}

public class UpdatePostRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = new();
    public List<string> TagNames { get; set; } = new();
}

public class ChangePostStatusRequest
{
    [Required]
    public string Action { get; set; } = string.Empty;
}

public class CommentResponse
{
    public int CommentID { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreateTime { get; set; }
    public int? UserID { get; set; }
    public string Username { get; set; } = string.Empty;
    public int? ParentCommentID { get; set; }
    public List<CommentResponse> Replies { get; set; } = new();
}

public class CreateCommentRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public int? ParentCommentID { get; set; }
}

public class FavoriteFolderResponse
{
    public int FolderID { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public DateTime? CreateTime { get; set; }
    public int PostCount { get; set; }
}

public class CreateFavoriteFolderRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string FolderName { get; set; } = string.Empty;
}

public class TagSuggestRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class AuditRecordResponse
{
    public int AuditID { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public int? TargetID { get; set; }
    public string TriggerWord { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreateTime { get; set; }
    public int? AuditorID { get; set; }
    public PostListItemResponse? Post { get; set; }
    public CommentResponse? Comment { get; set; }
}
