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
    public int? Credit { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public int UserLevel { get; set; } = 1;
    public int TotalCredit { get; set; } = 0;
    
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
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
    public DateTime? SendTime { get; set; }
    public DateTime? ExpireTime { get; set; }
    [MaxLength(1)]
    public string? IsUsed { get; set; }
}

[Table("PostTag")]
public class PostTag
{
    [Key]
    public int TagID { get; set; }
    [MaxLength(50)]
    public string? TagName { get; set; }
    public DateTime? CreateTime { get; set; }
}

[Table("Wallet")]
public class Wallet
{
    [Key]
    public int WalletID { get; set; }
    public decimal? Balance { get; set; }
    [MaxLength(255)]
    public string? PayPassword { get; set; }
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
}

[Table("Post")]
public class Post
{
    [Key]
    public int PostID { get; set; }
    [MaxLength(200)]
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int? HeatScore { get; set; }
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
    public decimal? Price { get; set; }
    public int? Stock { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public DateTime? PublishTime { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
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
}

[Table("PrivateMessage")]
public class PrivateMessage
{
    [Key]
    public int MessageID { get; set; }
    [MaxLength(4000)]
    public string? Content { get; set; }
    public DateTime? SendTime { get; set; }
    [MaxLength(1)]
    public string? IsRead { get; set; }
    public int? ReceiverID { get; set; }
    [ForeignKey("ReceiverID")]
    public User? Receiver { get; set; }
    public int? SenderID { get; set; }
    [ForeignKey("SenderID")]
    public User? Sender { get; set; }
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
    public int? TransactionID { get; set; }
    [ForeignKey("TransactionID")]
    public Transaction? Transaction { get; set; }
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public User? User { get; set; }
}
