using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<EmailCode> EmailCodes => Set<EmailCode>();
    public DbSet<PostTag> PostTags => Set<PostTag>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Forum> Forums => Set<Forum>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<TagPost> TagPosts => Set<TagPost>();
    public DbSet<FavoriteFolder> FavoriteFolders => Set<FavoriteFolder>();
    public DbSet<FolderPost> FolderPosts => Set<FolderPost>();
    public DbSet<ForumManager> ForumManagers => Set<ForumManager>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<DisputeTicket> DisputeTickets => Set<DisputeTicket>();
    public DbSet<ArbitrationResult> ArbitrationResults => Set<ArbitrationResult>();
    public DbSet<OrderMessage> OrderMessages => Set<OrderMessage>();
    public DbSet<ReportTicket> ReportTickets => Set<ReportTicket>();
    public DbSet<PrivateMessage> PrivateMessages => Set<PrivateMessage>();
    public DbSet<FriendShip> FriendShips => Set<FriendShip>();
    public DbSet<CreditAdjustment> CreditAdjustments => Set<CreditAdjustment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("User");
            e.HasKey(x => x.UserID);
            e.Property(x => x.UserID).HasColumnName("userId").ValueGeneratedOnAdd();
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PasswordHash).HasColumnName("passwordHash");
            e.Property(x => x.UserCode).HasColumnName("userCode");
            e.Property(x => x.Nickname).HasColumnName("nickname");
            e.Property(x => x.AvatarUrl).HasColumnName("avatarUrl");
            e.Property(x => x.Contact).HasColumnName("contact");
            e.Property(x => x.Bio).HasColumnName("bio");
            e.Property(x => x.Credit).HasColumnName("credit");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.UserLevel).HasColumnName("userLevel");
            e.Property(x => x.TotalCredit).HasColumnName("totalCredit");
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Role");
            e.HasKey(x => x.RoleID);
            e.Property(x => x.RoleID).HasColumnName("roleId").ValueGeneratedOnAdd();
            e.Property(x => x.RoleName).HasColumnName("roleName");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("Permission");
            e.HasKey(x => x.PermissionID);
            e.Property(x => x.PermissionID).HasColumnName("permissionId").ValueGeneratedOnAdd();
            e.Property(x => x.PermissionName).HasColumnName("permissionName");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Resource).HasColumnName("resource");
            e.Property(x => x.Action).HasColumnName("action");
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("UserRole");
            e.HasKey(x => new { x.UserID, x.RoleID });
            e.Property(x => x.UserID).HasColumnName("userId");
            e.Property(x => x.RoleID).HasColumnName("roleId");
            e.Property(x => x.AssignTime).HasColumnName("assignTime");
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermission");
            e.HasKey(x => new { x.RoleID, x.PermissionID });
            e.Property(x => x.RoleID).HasColumnName("roleId");
            e.Property(x => x.PermissionID).HasColumnName("permissionId");
        });

        modelBuilder.Entity<EmailCode>(e =>
        {
            e.ToTable("EmailCode");
            e.HasKey(x => x.EmailCodeID);
            e.Property(x => x.EmailCodeID).HasColumnName("emailCodeId").ValueGeneratedOnAdd();
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Code).HasColumnName("code");
            e.Property(x => x.SendTime).HasColumnName("sendTime");
            e.Property(x => x.ExpireTime).HasColumnName("expireTime");
            e.Property(x => x.IsUsed).HasColumnName("isUsed");
        });

        modelBuilder.Entity<PostTag>(e =>
        {
            e.ToTable("PostTag");
            e.HasKey(x => x.TagID);
            e.Property(x => x.TagID).HasColumnName("tagId").ValueGeneratedOnAdd();
            e.Property(x => x.TagName).HasColumnName("tagName");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
        });

        modelBuilder.Entity<Wallet>(e =>
        {
            e.ToTable("Wallet");
            e.HasKey(x => x.WalletID);
            e.Property(x => x.WalletID).HasColumnName("walletId").ValueGeneratedOnAdd();
            e.Property(x => x.Balance).HasColumnName("balance").HasPrecision(18, 2);
            e.Property(x => x.PayPassword).HasColumnName("payPassword");
            e.Property(x => x.UserID).HasColumnName("userId");
        });

        modelBuilder.Entity<Forum>(e =>
        {
            e.ToTable("Forum");
            e.HasKey(x => x.ForumID);
            e.Property(x => x.ForumID).HasColumnName("forumId").ValueGeneratedOnAdd();
            e.Property(x => x.ForumName).HasColumnName("forumName");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.CreatorID).HasColumnName("creatorId");
        });

        modelBuilder.Entity<Post>(e =>
        {
            e.ToTable("Post");
            e.HasKey(x => x.PostID);
            e.Property(x => x.PostID).HasColumnName("postId").ValueGeneratedOnAdd();
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.ImageUrls).HasColumnName("imageUrls");
            e.Property(x => x.HeatScore).HasColumnName("heatScore");
            e.Property(x => x.LikeCount).HasColumnName("likeCount");
            e.Property(x => x.ViewCount).HasColumnName("viewCount");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.UpdateTime).HasColumnName("updateTime");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.UserID).HasColumnName("userId");
            e.Property(x => x.ForumID).HasColumnName("forumId");
        });

        modelBuilder.Entity<PostComment>(e =>
        {
            e.ToTable("PostComment");
            e.HasKey(x => x.CommentID);
            e.Property(x => x.CommentID).HasColumnName("commentId").ValueGeneratedOnAdd();
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.PostID).HasColumnName("postId");
            e.Property(x => x.UserID).HasColumnName("userId");
            e.Property(x => x.ParentCommentID).HasColumnName("parentCommentId");
        });

        modelBuilder.Entity<PostLike>(e =>
        {
            e.ToTable("PostLike");
            e.HasKey(x => x.LikeID);
            e.Property(x => x.LikeID).HasColumnName("likeId").ValueGeneratedOnAdd();
            e.Property(x => x.PostID).HasColumnName("postId");
            e.Property(x => x.UserID).HasColumnName("userId");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.HasIndex(x => new { x.PostID, x.UserID }).IsUnique();
        });

        modelBuilder.Entity<TagPost>(e =>
        {
            e.ToTable("TagPost");
            e.HasKey(x => new { x.PostID, x.TagID });
            e.Property(x => x.PostID).HasColumnName("postId");
            e.Property(x => x.TagID).HasColumnName("tagId");
        });

        modelBuilder.Entity<FavoriteFolder>(e =>
        {
            e.ToTable("FavoriteFolder");
            e.HasKey(x => x.FolderID);
            e.Property(x => x.FolderID).HasColumnName("folderId").ValueGeneratedOnAdd();
            e.Property(x => x.FolderName).HasColumnName("folderName");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.UserID).HasColumnName("userId");
        });

        modelBuilder.Entity<FolderPost>(e =>
        {
            e.ToTable("FolderPost");
            e.HasKey(x => new { x.FolderID, x.PostID });
            e.Property(x => x.FolderID).HasColumnName("folderId");
            e.Property(x => x.PostID).HasColumnName("postId");
        });

        modelBuilder.Entity<ForumManager>(e =>
        {
            e.ToTable("ForumManager");
            e.HasKey(x => new { x.ForumID, x.UserID });
            e.Property(x => x.ForumID).HasColumnName("forumId");
            e.Property(x => x.UserID).HasColumnName("userId");
        });

        modelBuilder.Entity<AuditRecord>(e =>
        {
            e.ToTable("AuditRecord");
            e.HasKey(x => x.AuditID);
            e.Property(x => x.AuditID).HasColumnName("auditId").ValueGeneratedOnAdd();
            e.Property(x => x.TargetType).HasColumnName("targetType");
            e.Property(x => x.TargetID).HasColumnName("targetId");
            e.Property(x => x.TriggerWord).HasColumnName("triggerWord");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.AuditorID).HasColumnName("auditorId");
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Product");
            e.HasKey(x => x.ProductID);
            e.Property(x => x.ProductID).HasColumnName("productId").ValueGeneratedOnAdd();
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.ImageUrls).HasColumnName("imageUrls");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 2);
            e.Property(x => x.Stock).HasColumnName("stock");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.PublishTime).HasColumnName("publishTime");
            e.Property(x => x.UserID).HasColumnName("userId");
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("Transaction");
            e.HasKey(x => x.TransactionID);
            e.Property(x => x.TransactionID).HasColumnName("transactionId").ValueGeneratedOnAdd();
            e.Property(x => x.TransactionAmount).HasColumnName("transactionAmount").HasPrecision(18, 2);
            e.Property(x => x.TransactionStatus).HasColumnName("transactionStatus");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.PayTime).HasColumnName("payTime");
            e.Property(x => x.UserID).HasColumnName("userId");
            e.Property(x => x.ProductID).HasColumnName("productId");
        });

        modelBuilder.Entity<DisputeTicket>(e =>
        {
            e.ToTable("DisputeTicket");
            e.HasKey(x => x.TicketID);
            e.Property(x => x.TicketID).HasColumnName("ticketId").ValueGeneratedOnAdd();
            e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.AssignTime).HasColumnName("assignTime");
            e.Property(x => x.TransactionID).HasColumnName("transactionId");
            e.Property(x => x.UserID).HasColumnName("userId");
            e.Property(x => x.ArbitratorID).HasColumnName("arbitratorId");
        });

        modelBuilder.Entity<ArbitrationResult>(e =>
        {
            e.ToTable("ArbitrationResult");
            e.HasKey(x => x.ResultID);
            e.Property(x => x.ResultID).HasColumnName("resultId").ValueGeneratedOnAdd();
            e.Property(x => x.Decision).HasColumnName("decision");
            e.Property(x => x.RefundAmount).HasColumnName("refundAmount").HasPrecision(18, 2);
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.DisputeTicketID).HasColumnName("disputeTicketId");
            e.Property(x => x.WalletID).HasColumnName("walletId");
        });

        modelBuilder.Entity<OrderMessage>(e =>
        {
            e.ToTable("OrderMessage");
            e.HasKey(x => x.OrderMessageID);
            e.Property(x => x.OrderMessageID).HasColumnName("orderMessageId").ValueGeneratedOnAdd();
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.SendTime).HasColumnName("sendTime");
            e.Property(x => x.IsArchived).HasColumnName("isArchived");
            e.Property(x => x.TransactionID).HasColumnName("transactionId");
            e.Property(x => x.SenderID).HasColumnName("senderId");
        });

        modelBuilder.Entity<ReportTicket>(e =>
        {
            e.ToTable("ReportTicket");
            e.HasKey(x => x.ReportID);
            e.Property(x => x.ReportID).HasColumnName("reportId").ValueGeneratedOnAdd();
            e.Property(x => x.TargetType).HasColumnName("targetType");
            e.Property(x => x.TargetID).HasColumnName("targetId");
            e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.ReviewTime).HasColumnName("reviewTime");
            e.Property(x => x.Result).HasColumnName("result");
            e.Property(x => x.ReporterID).HasColumnName("reporterId");
            e.Property(x => x.ReviewerID).HasColumnName("reviewerId");
        });

        modelBuilder.Entity<PrivateMessage>(e =>
        {
            e.ToTable("PrivateMessage");
            e.HasKey(x => x.MessageID);
            e.Property(x => x.MessageID).HasColumnName("messageId").ValueGeneratedOnAdd();
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.SendTime).HasColumnName("sendTime");
            e.Property(x => x.IsRead).HasColumnName("isRead");
            e.Property(x => x.ReceiverID).HasColumnName("receiverId");
            e.Property(x => x.SenderID).HasColumnName("senderId");
        });

        modelBuilder.Entity<FriendShip>(e =>
        {
            e.ToTable("FriendShip");
            e.HasKey(x => x.FriendshipID);
            e.Property(x => x.FriendshipID).HasColumnName("friendshipId").ValueGeneratedOnAdd();
            e.Property(x => x.UserID).HasColumnName("userId");
            e.Property(x => x.FriendID).HasColumnName("friendId");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.UpdateTime).HasColumnName("updateTime");
            e.HasOne(x => x.User)
                .WithMany(u => u.FriendShips)
                .HasForeignKey(x => x.UserID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Friend)
                .WithMany()
                .HasForeignKey(x => x.FriendID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditAdjustment>(e =>
        {
            e.ToTable("CreditAdjustment");
            e.HasKey(x => x.CreditAdjustmentID);
            e.Property(x => x.CreditAdjustmentID).HasColumnName("creditAdjustmentId").ValueGeneratedOnAdd();
            e.Property(x => x.UserID).HasColumnName("userId");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.ChangePoints).HasColumnName("changePoints");
            e.Property(x => x.AdjustTime).HasColumnName("adjustTime");
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notification");
            e.HasKey(x => x.NotificationID);
            e.Property(x => x.NotificationID).HasColumnName("notificationId").ValueGeneratedOnAdd();
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.CreateTime).HasColumnName("createTime");
            e.Property(x => x.TransactionID).HasColumnName("transactionId");
            e.Property(x => x.UserID).HasColumnName("userId");
        });
    }
}
