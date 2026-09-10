using System.ComponentModel.DataAnnotations;

namespace Backend.Models.DTOs;

public class ProductResponse
{
    public int ProductID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public int? UserID { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();
}

public class CreateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = new();

    [StringLength(50)]
    public string Category { get; set; } = "其他";

    [StringLength(50)]
    public string Condition { get; set; } = "良好";

    [Range(0.01, 99999999)]
    public decimal Price { get; set; }

    [Range(1, 999999)]
    public int Stock { get; set; } = 1;
}

public class UpdateProductRequest : CreateProductRequest
{
    public string Status { get; set; } = "Active";
}

public class ChangeProductStatusRequest
{
    [Required]
    public string Action { get; set; } = string.Empty;
}

public class TransactionResponse
{
    public int TransactionID { get; set; }
    public decimal TransactionAmount { get; set; }
    public string TransactionStatus { get; set; } = string.Empty;
    public DateTime? CreateTime { get; set; }
    public DateTime? PayTime { get; set; }
    public int? UserID { get; set; }
    public int? ProductID { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public int? SellerID { get; set; }
    public string SellerName { get; set; } = string.Empty;
}

public class CreateTransactionRequest
{
    [Required]
    public int ProductID { get; set; }
}

public class CreateDisputeRequest
{
    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string Reason { get; set; } = string.Empty;
}

public class DisputeTicketResponse
{
    public int TicketID { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public DateTime? CreateTime { get; set; }
    public DateTime? AssignTime { get; set; }
    public int? TransactionID { get; set; }
    public decimal TransactionAmount { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public int? BuyerID { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public int? SellerID { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public int? UserID { get; set; }
    public string Username { get; set; } = string.Empty;
    public int? ArbitratorID { get; set; }
    public string ArbitratorName { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public decimal? RefundAmount { get; set; }
    public DateTime? ResolvedTime { get; set; }
}

public class ResolveDisputeRequest
{
    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string Decision { get; set; } = string.Empty;

    [Range(0, 99999999)]
    public decimal RefundAmount { get; set; }

    [StringLength(20)]
    public string? ResponsibilityParty { get; set; }
}

public class WalletResponse
{
    public int WalletID { get; set; }
    public decimal Balance { get; set; }
    public int? UserID { get; set; }
}

public class DepositWalletRequest
{
    [Range(0.01, 99999999)]
    public decimal Amount { get; set; }
}

public class OrderMessageResponse
{
    public int OrderMessageID { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime? SendTime { get; set; }
    public bool IsArchived { get; set; }
    public int? TransactionID { get; set; }
    public int? SenderID { get; set; }
    public string SenderName { get; set; } = string.Empty;
}

public class CreateOrderMessageRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

public class ReportTicketResponse
{
    public int ReportID { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public int? TargetID { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CreateTime { get; set; }
    public DateTime? ReviewTime { get; set; }
    public string Result { get; set; } = string.Empty;
    public int? ReporterID { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public int? ReviewerID { get; set; }
    public string? ReviewerName { get; set; }
    /// <summary>被举报内容的快照（详情接口返回，含标题/内容/状态等）</summary>
    public ReportTargetSnapshot? Target { get; set; }
}

public class ReportTargetSnapshot
{
    public int TargetID { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Status { get; set; }
    public int? OwnerID { get; set; }
    public string? OwnerName { get; set; }
    public int? OwnerCredit { get; set; }
}

public class CreateReportRequest
{
    [Required]
    public string TargetType { get; set; } = string.Empty;

    [Required]
    public int TargetID { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}

public class ReviewReportRequest
{
    [Required]
    public string Action { get; set; } = string.Empty;

    [StringLength(500)]
    public string Result { get; set; } = string.Empty;
}
