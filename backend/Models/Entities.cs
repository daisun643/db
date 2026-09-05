using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

[Table("\"User\"")]
public class User
{
    [Key]
    public int UserID { get; set; }
    [MaxLength(50)]
    public string? Username { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
    [MaxLength(255)]
    public string? PasswordHash { get; set; }
    [MaxLength(50)]
    public string? UserCode { get; set; }
    [MaxLength(100)]
    public string? Contact { get; set; }
    [MaxLength(500)]
    public string? Bio { get; set; }
    public int? Credit { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    [MaxLength(64)]
    public string? SessionVersion { get; set; }

    public ICollection<MediaFile> UploadedMedia { get; set; } = new List<MediaFile>();
    public UserAvatar? AvatarMedia { get; set; }
    [NotMapped]
    public string? AvatarUrl => AvatarMedia?.Media?.Url;
    
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<PostLike> PostLikes { get; set; } = new List<PostLike>();
    public ICollection<FavoriteFolder> FavoriteFolders { get; set; } = new List<FavoriteFolder>();
    public ICollection<FriendShip> FriendShips { get; set; } = new List<FriendShip>();
    public ICollection<CreditAdjustment> CreditAdjustments { get; set; } = new List<CreditAdjustment>();
}

[Table("MediaFile")]
public class MediaFile
{
    [Key]
    public int MediaID { get; set; }
    [MaxLength(50)]
    public string StorageProvider { get; set; } = "s3";
    [MaxLength(500)]
    public string? ObjectKey { get; set; }
    [MaxLength(4000)]
    public string? FileName { get; set; }
    [MaxLength(4000)]
    public string? OriginalFileName { get; set; }
    [MaxLength(500)]
    public string? Url { get; set; }
    [MaxLength(100)]
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    [MaxLength(64)]
    public string? ContentHash { get; set; }
    public DateTime? UploadTime { get; set; }
    public int? UploadedByUserID { get; set; }
    [ForeignKey("UploadedByUserID")]
    public User? UploadedByUser { get; set; }
    public ICollection<PostMedia> PostLinks { get; set; } = new List<PostMedia>();
    public ICollection<ProductMedia> ProductLinks { get; set; } = new List<ProductMedia>();
    public UserAvatar? AvatarLink { get; set; }
}

[Table("UserAvatar")]
public class UserAvatar
{
    [Key]
    public int UserID { get; set; }
    [ForeignKey(nameof(UserID))]
    public User? User { get; set; }
    public int MediaID { get; set; }
    [ForeignKey(nameof(MediaID))]
    public MediaFile? Media { get; set; }
}

[Table("ForumAvatar")]
public class ForumAvatar
{
    [Key]
    public int ForumID { get; set; }
    [ForeignKey(nameof(ForumID))]
    public Forum? Forum { get; set; }
    public int MediaID { get; set; }
    [ForeignKey(nameof(MediaID))]
    public MediaFile? Media { get; set; }
}

[Table("Role")]
public class Role
{
    [Key]
    public int RoleID { get; set; }
    [MaxLength(50)]
    public string? RoleName { get; set; }
    [MaxLength(200)]
    public string? Description { get; set; }
    public DateTime? CreateTime { get; set; }
    
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

[Table("Permission")]
public class Permission
{
    [Key]
    public int PermissionID { get; set; }
    [MaxLength(100)]
    public string? PermissionName { get; set; }
    [MaxLength(200)]
    public string? Description { get; set; }
    [MaxLength(50)]
    public string? Resource { get; set; }
    [MaxLength(50)]
    public string? Action { get; set; }
    
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

[Table("UserRole")]
public class UserRole
{
    public int UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    
    public int RoleID { get; set; }
    [ForeignKey("RoleID")]
    public Role? Role { get; set; }
    
    public DateTime? AssignTime { get; set; }
}

[Table("RolePermission")]
public class RolePermission
{
    public int RoleID { get; set; }
    [ForeignKey("RoleID")]
    public Role? Role { get; set; }
    
    public int PermissionID { get; set; }
    [ForeignKey("PermissionID")]
    public Permission? Permission { get; set; }
}

[Table("EmailCode")]
public class EmailCode
{
    [Key]
    public int EmailCodeID { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
    [MaxLength(10)]
    public string? Code { get; set; }
    [MaxLength(30)]
    public string? Purpose { get; set; }
    public DateTime? SendTime { get; set; }
    public DateTime? ExpireTime { get; set; }
    [MaxLength(1)]
    public string? IsUsed { get; set; }
}

[Table("Wallet")]
public class Wallet
{
    [Key]
    public int WalletID { get; set; }
    public decimal? Balance { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
}

[Table("Forum")]
public class Forum
{
    [Key]
    public int ForumID { get; set; }
    [MaxLength(100)]
    public string? ForumName { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public DateTime? CreateTime { get; set; }
    public int? CreatorID { get; set; }
    [ForeignKey("CreatorID")]
    public User? Creator { get; set; }
    public ForumAvatar? AvatarMedia { get; set; }
    [NotMapped]
    public string? AvatarUrl => AvatarMedia?.Media?.Url;

    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<ForumManager> ForumManagers { get; set; } = new List<ForumManager>();
}

[Table("Post")]
public class Post
{
    [Key]
    public int PostID { get; set; }
    [MaxLength(200)]
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int? LikeCount { get; set; }
    public int? ViewCount { get; set; }
    public DateTime? CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    public int? ForumID { get; set; }
    [ForeignKey("ForumID")]
    public Forum? Forum { get; set; }

    public ICollection<PostComment> Comments { get; set; } = new List<PostComment>();
    public ICollection<PostLike> Likes { get; set; } = new List<PostLike>();
    public ICollection<FolderPost> FolderPosts { get; set; } = new List<FolderPost>();
    public ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();
}

[Table("PostMedia")]
public class PostMedia
{
    public int PostID { get; set; }
    [ForeignKey(nameof(PostID))]
    public Post? Post { get; set; }
    public int MediaID { get; set; }
    [ForeignKey(nameof(MediaID))]
    public MediaFile? Media { get; set; }
    public int DisplayOrder { get; set; }
}

[Table("PostComment")]
public class PostComment
{
    [Key]
    public int CommentID { get; set; }
    [MaxLength(4000)]
    public string? Content { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public DateTime? CreateTime { get; set; }
    public int? PostID { get; set; }
    [ForeignKey("PostID")]
    public Post? Post { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    public int? ParentCommentID { get; set; }
    [ForeignKey("ParentCommentID")]
    public PostComment? ParentComment { get; set; }

    public ICollection<PostComment> Replies { get; set; } = new List<PostComment>();
}

[Table("FavoriteFolder")]
public class FavoriteFolder
{
    [Key]
    public int FolderID { get; set; }
    [MaxLength(100)]
    public string? FolderName { get; set; }
    public DateTime? CreateTime { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }

    public ICollection<FolderPost> FolderPosts { get; set; } = new List<FolderPost>();
}

[Table("AuditRecord")]
public class AuditRecord
{
    [Key]
    public int AuditID { get; set; }
    [MaxLength(50)]
    public string? TargetType { get; set; }
    public int? TargetID { get; set; }
    [MaxLength(200)]
    public string? TriggerWord { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public DateTime? CreateTime { get; set; }
    public int? AuditorID { get; set; }
    [ForeignKey("AuditorID")]
    public User? Auditor { get; set; }
}

[Table("ForumManager")]
public class ForumManager
{
    public int ForumID { get; set; }
    [ForeignKey("ForumID")]
    public Forum? Forum { get; set; }

    public int UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }

    // Moderator=版主，Admin=管理员
    [MaxLength(20)]
    public string? Role { get; set; }
}

[Table("ForumMember")]
public class ForumMember
{
    public int ForumID { get; set; }
    [ForeignKey("ForumID")]
    public Forum? Forum { get; set; }

    public int UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }

    public DateTime? JoinTime { get; set; }
}

[Table("PostLike")]
public class PostLike
{
    [Key]
    public int LikeID { get; set; }
    public int? PostID { get; set; }
    [ForeignKey("PostID")]
    public Post? Post { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    public DateTime? CreateTime { get; set; }
}

[Table("FolderPost")]
public class FolderPost
{
    public int FolderID { get; set; }
    [ForeignKey("FolderID")]
    public FavoriteFolder? Folder { get; set; }

    public int PostID { get; set; }
    [ForeignKey("PostID")]
    public Post? Post { get; set; }
}

[Table("Product")]
public class Product
{
    [Key]
    public int ProductID { get; set; }
    [MaxLength(200)]
    public string? Title { get; set; }
    [MaxLength(4000)]
    public string? Description { get; set; }
    public int CategoryID { get; set; }
    [ForeignKey(nameof(CategoryID))]
    public ProductCategory? Category { get; set; }
    public int ConditionID { get; set; }
    [ForeignKey(nameof(ConditionID))]
    public ProductCondition? Condition { get; set; }
    public decimal? Price { get; set; }
    public int? Stock { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public DateTime? PublishTime { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    public ICollection<ProductMedia> Media { get; set; } = new List<ProductMedia>();
}

[Table("ProductCategory")]
public class ProductCategory
{
    [Key]
    public int CategoryID { get; set; }
    [Required, MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

[Table("ProductCondition")]
public class ProductCondition
{
    [Key]
    public int ConditionID { get; set; }
    [Required, MaxLength(50)]
    public string ConditionName { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

[Table("ProductMedia")]
public class ProductMedia
{
    public int ProductID { get; set; }
    [ForeignKey(nameof(ProductID))]
    public Product? Product { get; set; }
    public int MediaID { get; set; }
    [ForeignKey(nameof(MediaID))]
    public MediaFile? Media { get; set; }
    public int DisplayOrder { get; set; }
}

[Table("Transaction")]
public class Transaction
{
    [Key]
    public int TransactionID { get; set; }
    public decimal? TransactionAmount { get; set; }
    [MaxLength(20)]
    public string? TransactionStatus { get; set; }
    public DateTime? CreateTime { get; set; }
    public DateTime? PayTime { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    public int? ProductID { get; set; }
    [ForeignKey("ProductID")]
    public Product? Product { get; set; }

    public ICollection<DisputeTicket> DisputeTickets { get; set; } = new List<DisputeTicket>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<OrderMessage> OrderMessages { get; set; } = new List<OrderMessage>();
}

[Table("DisputeTicket")]
public class DisputeTicket
{
    [Key]
    public int TicketID { get; set; }
    [MaxLength(500)]
    public string? Reason { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public DateTime? CreateTime { get; set; }
    public DateTime? AssignTime { get; set; }
    public int? TransactionID { get; set; }
    [ForeignKey("TransactionID")]
    public Transaction? Transaction { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    public int? ArbitratorID { get; set; }
    [ForeignKey("ArbitratorID")]
    public User? Arbitrator { get; set; }
    public ICollection<ArbitrationResult> ArbitrationResults { get; set; } = new List<ArbitrationResult>();
}

[Table("ArbitrationResult")]
public class ArbitrationResult
{
    [Key]
    public int ResultID { get; set; }
    [MaxLength(500)]
    public string? Decision { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime? CreateTime { get; set; }
    public int? DisputeTicketID { get; set; }
    [ForeignKey("DisputeTicketID")]
    public DisputeTicket? DisputeTicket { get; set; }
    public int? WalletID { get; set; }
    [ForeignKey("WalletID")]
    public Wallet? Wallet { get; set; }
}

[Table("OrderMessage")]
public class OrderMessage
{
    [Key]
    public int OrderMessageID { get; set; }
    [MaxLength(4000)]
    public string? Content { get; set; }
    public DateTime? SendTime { get; set; }
    [MaxLength(1)]
    public string? IsArchived { get; set; }
    public int? TransactionID { get; set; }
    [ForeignKey("TransactionID")]
    public Transaction? Transaction { get; set; }
    public int? SenderID { get; set; }
    [ForeignKey("SenderID")]
    public User? Sender { get; set; }
}

[Table("ReportTicket")]
public class ReportTicket
{
    [Key]
    public int ReportID { get; set; }
    [MaxLength(50)]
    public string? TargetType { get; set; }
    public int? TargetID { get; set; }
    [MaxLength(500)]
    public string? Reason { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public DateTime? CreateTime { get; set; }
    public DateTime? ReviewTime { get; set; }
    [MaxLength(500)]
    public string? Result { get; set; }
    public int? ReporterID { get; set; }
    [ForeignKey("ReporterID")]
    public User? Reporter { get; set; }
    public int? ReviewerID { get; set; }
    [ForeignKey("ReviewerID")]
    public User? Reviewer { get; set; }
}

[Table("PrivateMessage")]
public class PrivateMessage
{
    [Key]
    public int MessageID { get; set; }
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;
    public DateTime SendTime { get; set; }
    [Required]
    [MaxLength(1)]
    public string IsRead { get; set; } = "0";
    public int ReceiverID { get; set; }
    [ForeignKey("ReceiverID")]
    public User? Receiver { get; set; }
    public int SenderID { get; set; }
    [ForeignKey("SenderID")]
    public User? Sender { get; set; }
}

[Table("FriendShip")]
public class FriendShip
{
    [Key]
    public int FriendshipID { get; set; }
    public int UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    public int FriendID { get; set; }
    [ForeignKey("FriendID")]
    public User? Friend { get; set; }
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

[Table("CreditAdjustment")]
public class CreditAdjustment
{
    [Key]
    public int CreditAdjustmentID { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public int? ChangePoints { get; set; }
    public int? BeforeCredit { get; set; }
    public int? AfterCredit { get; set; }
    public int? OperatorID { get; set; }
    [ForeignKey("OperatorID")]
    public User? Operator { get; set; }
    public DateTime? AdjustTime { get; set; }
}

[Table("Notification")]
public class Notification
{
    [Key]
    public int NotificationID { get; set; }
    [MaxLength(200)]
    public string? Title { get; set; }
    [MaxLength(4000)]
    public string? Content { get; set; }
    public DateTime? CreateTime { get; set; }
    [MaxLength(50)]
    public string? Type { get; set; }
    [MaxLength(50)]
    public string? TargetType { get; set; }
    public int? TargetID { get; set; }
    [MaxLength(500)]
    public string? Link { get; set; }
    public string? IsRead { get; set; }
    public DateTime? ReadTime { get; set; }
    [MaxLength(200)]
    public string? EventKey { get; set; }
    public int? TransactionID { get; set; }
    [ForeignKey("TransactionID")]
    public Transaction? Transaction { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
}
