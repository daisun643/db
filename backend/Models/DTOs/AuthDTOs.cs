using System.ComponentModel.DataAnnotations;

namespace Backend.Models.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "用户名长度必须在2-50个字符之间")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "密码摘要不能为空")]
    [StringLength(64, MinimumLength = 64, ErrorMessage = "密码摘要必须是64位SHA-256十六进制字符串")]
    [RegularExpression("^[a-f0-9]{64}$", ErrorMessage = "密码摘要格式不正确")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "验证码不能为空")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "验证码必须是6位数字")]
    public string Code { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "密码摘要不能为空")]
    [StringLength(64, MinimumLength = 64, ErrorMessage = "密码摘要必须是64位SHA-256十六进制字符串")]
    [RegularExpression("^[a-f0-9]{64}$", ErrorMessage = "密码摘要格式不正确")]
    public string Password { get; set; } = string.Empty;
}

public class SendCodeRequest
{
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "验证码不能为空")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "验证码必须是6位数字")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "新密码摘要不能为空")]
    [StringLength(64, MinimumLength = 64, ErrorMessage = "新密码摘要必须是64位SHA-256十六进制字符串")]
    [RegularExpression("^[a-f0-9]{64}$", ErrorMessage = "新密码摘要格式不正确")]
    public string NewPassword { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserInfo? User { get; set; }
}

public class UserInfo
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Credit { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RouteAccessRequest
{
    [Required(ErrorMessage = "路径不能为空")]
    public string Path { get; set; } = string.Empty;
}
