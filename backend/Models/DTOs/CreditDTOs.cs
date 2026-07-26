using System.ComponentModel.DataAnnotations;

namespace Backend.Models.DTOs;

public class AddCreditRequest
{
    public int UserId { get; set; }
    public int Credit { get; set; }
    public int? OperatorId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class UserLevelResponse
{
    public int UserId { get; set; }
    public int CurrentLevel { get; set; }
    public int TotalCredit { get; set; }
    public int NextLevelRequirement { get; set; }
    public int CreditToNextLevel { get; set; }
}

public class UserCreditResponse
{
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public int UserLevel { get; set; }
    public int TotalCredit { get; set; }
    public int Credit { get; set; }
}

public class CreditAdjustmentResponse
{
    public int CreditAdjustmentId { get; set; }
    public int? UserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int ChangePoints { get; set; }
    public int? BeforeCredit { get; set; }
    public int? AfterCredit { get; set; }
    public int? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public DateTime? AdjustTime { get; set; }
}

public class UpdateProfileRequest
{
    [StringLength(50, MinimumLength = 2, ErrorMessage = "用户名长度必须在2-50个字符之间")]
    public string? Username { get; set; }

    [StringLength(50, ErrorMessage = "昵称长度不能超过50个字符")]
    public string? Nickname { get; set; }

    [StringLength(500, ErrorMessage = "头像链接长度不能超过500个字符")]
    public string? AvatarUrl { get; set; }

    [StringLength(100, ErrorMessage = "联系方式长度不能超过100个字符")]
    public string? Contact { get; set; }

    [StringLength(500, ErrorMessage = "个人简介长度不能超过500个字符")]
    public string? Bio { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "当前密码不能为空")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "新密码不能为空")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "新密码长度必须在8-100个字符之间")]
    public string NewPassword { get; set; } = string.Empty;
}
