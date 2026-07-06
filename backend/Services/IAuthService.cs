using Backend.Models;
using Backend.Models.DTOs;

namespace Backend.Services;

public interface IAuthService
{
    Task<(bool Success, string Message, string? DebugCode)> SendVerificationCodeAsync(string email);
    Task<(bool Success, string Message, User? User)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string Message, User? User, List<string>? Roles, List<string>? Permissions)> LoginAsync(LoginRequest request);
    Task<(bool Success, string Message)> ForgotPasswordAsync(string email);
    Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequest request);
    bool ValidateEmailDomain(string email);
}
