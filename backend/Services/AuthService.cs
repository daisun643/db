using Backend.Configuration;
using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BCrypt.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Backend.Services;

public class AuthService : IAuthService
{
    private const string RegisterCodePurpose = "Register";
    private const string PasswordResetCodePurpose = "PasswordReset";
    private const string PasswordResetAcceptedMessage = "如果该邮箱已注册，重置验证码将发送到您的邮箱";
    private const string InvalidOrExpiredCodeMessage = "验证码无效或已过期";

    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;
    private readonly AuthSettings _authSettings;
    private readonly ILogger<AuthService> _logger;
    private readonly IWebHostEnvironment _environment;

    public AuthService(
        AppDbContext db,
        IEmailService emailService,
        IOptions<EmailSettings> emailSettings,
        IOptions<AuthSettings> authSettings,
        ILogger<AuthService> logger,
        IWebHostEnvironment environment)
    {
        _db = db;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
        _authSettings = authSettings.Value;
        _logger = logger;
        _environment = environment;
    }

    public bool ValidateEmailDomain(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        
        var domain = email.Split('@').LastOrDefault();
        return domain?.Equals(_emailSettings.AllowedDomain, StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<(bool Success, string Message, string? DebugCode)> SendVerificationCodeAsync(string email)
    {
        email = NormalizeEmail(email);

        if (!ValidateEmailDomain(email))
        {
            return (false, $"仅支持 @{_emailSettings.AllowedDomain} 邮箱注册", null);
        }

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);
        if (existingUser != null)
        {
            return (false, "该邮箱已被注册", null);
        }

        var code = GenerateVerificationCode();
        var expireTime = DateTime.Now.AddMinutes(_authSettings.CodeExpirationMinutes);

        await MarkUnusedCodesAsUsedAsync(email, RegisterCodePurpose);

        var emailCode = new EmailCode
        {
            Email = email,
            Code = code,
            Purpose = RegisterCodePurpose,
            SendTime = DateTime.Now,
            ExpireTime = expireTime,
            IsUsed = "0"
        };

        _db.EmailCodes.Add(emailCode);
        await _db.SaveChangesAsync();

        var sent = await _emailService.SendVerificationCodeAsync(email, code);
        if (!sent)
        {
            if (!_environment.IsDevelopment())
            {
                return (false, "验证码发送失败，请稍后重试", null);
            }

            _logger.LogWarning("开发环境邮件发送失败，返回调试验证码用于本地测试: {Email}", email);
        }

        return (true, "验证码已发送到您的邮箱", GetDebugCode(code));
    }

    public async Task<(bool Success, string Message, User? User)> RegisterAsync(RegisterRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var username = request.Username.Trim();

        if (username.Length < 2 || username.Length > 50)
        {
            return (false, "用户名长度必须在2-50个字符之间", null);
        }

        if (username.Any(char.IsWhiteSpace))
        {
            return (false, "用户名不能包含空格", null);
        }

        if (!ValidateEmailDomain(email))
        {
            return (false, $"仅支持 @{_emailSettings.AllowedDomain} 邮箱注册", null);
        }

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);
        if (existingUser != null)
        {
            return (false, "该邮箱已被注册", null);
        }

        var existingUsername = await _db.Users.FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == username.ToLower());
        if (existingUsername != null)
        {
            return (false, "该用户名已被使用", null);
        }

        var emailCode = await _db.EmailCodes
            .Where(ec => ec.Email != null &&
                         ec.Email.ToLower() == email &&
                         ec.Code == request.Code &&
                         ec.Purpose == RegisterCodePurpose &&
                         ec.IsUsed == "0")
            .OrderByDescending(ec => ec.SendTime)
            .FirstOrDefaultAsync();

        if (emailCode == null)
        {
            return (false, "验证码无效", null);
        }

        if (emailCode.ExpireTime < DateTime.Now)
        {
            return (false, "验证码已过期", null);
        }

        if (!ValidatePassword(request.Password))
        {
            return (false, "密码必须包含大小写字母和数字", null);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            Credit = 100,
            Status = "Active",
            UserCode = GenerateUserCode(),
            TotalCredit = 0
        };

        _db.Users.Add(user);
        
        emailCode.IsUsed = "1";
        
        await _db.SaveChangesAsync();

        var defaultRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
        if (defaultRole != null)
        {
            var userRole = new UserRole
            {
                UserID = user.UserID,
                RoleID = defaultRole.RoleID,
                AssignTime = DateTime.Now
            };
            _db.UserRoles.Add(userRole);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("用户注册成功并分配默认角色: {Email}", email);
        }
        else
        {
            _logger.LogWarning("用户注册成功但未找到默认角色: {Email}", email);
        }

        return (true, "注册成功", user);
    }

    public async Task<(bool Success, string Message, User? User, List<string>? Roles, List<string>? Permissions)> LoginAsync(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);

        var user = await _db.Users
            .Include(u => u.AvatarMedia)
            .ThenInclude(a => a!.Media)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r!.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);
        
        if (user == null)
        {
            return (false, "邮箱或密码错误", null, null, null);
        }

        if (user.Status != "Active")
        {
            return (false, "账号已被禁用", null, null, null);
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return (false, "邮箱或密码错误", null, null, null);
        }

        var roles = user.UserRoles
            .Select(ur => ur.Role?.RoleName)
            .Where(r => r != null)
            .Cast<string>()
            .ToList();

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role?.RolePermissions ?? new List<RolePermission>())
            .Select(rp => rp.Permission?.PermissionName)
            .Where(p => p != null)
            .Cast<string>()
            .Distinct()
            .ToList();

        _logger.LogInformation("用户登录成功: {Email}, 角色: {Roles}, 权限数: {PermissionCount}", 
            email, string.Join(", ", roles), permissions.Count);
        
        return (true, "登录成功", user, roles, permissions);
    }

    public async Task<(bool Success, string Message, string? DebugCode)> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = NormalizeEmail(request.Email);

        if (!ValidateEmailDomain(email))
        {
            return (false, $"仅支持 @{_emailSettings.AllowedDomain} 邮箱", null);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);
        if (user == null)
        {
            return (true, PasswordResetAcceptedMessage, GetDebugCode(GenerateVerificationCode()));
        }

        var code = GenerateVerificationCode();
        var expireMinutes = _environment.IsDevelopment() && request.DebugExpiresInMinutes.HasValue
            ? request.DebugExpiresInMinutes.Value
            : _authSettings.CodeExpirationMinutes;
        var expireTime = DateTime.Now.AddMinutes(expireMinutes);

        await MarkUnusedCodesAsUsedAsync(email, PasswordResetCodePurpose);

        var emailCode = new EmailCode
        {
            Email = email,
            Code = code,
            Purpose = PasswordResetCodePurpose,
            SendTime = DateTime.Now,
            ExpireTime = expireTime,
            IsUsed = "0"
        };

        _db.EmailCodes.Add(emailCode);
        await _db.SaveChangesAsync();

        var sent = await _emailService.SendPasswordResetCodeAsync(email, code);
        if (!sent)
        {
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning("密码重置邮件发送失败，但对外返回统一响应: {Email}", email);
                return (true, PasswordResetAcceptedMessage, null);
            }

            _logger.LogWarning("开发环境密码重置邮件发送失败，返回调试验证码用于本地测试: {Email}", email);
        }

        return (true, PasswordResetAcceptedMessage, GetDebugCode(code));
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var email = NormalizeEmail(request.Email);

        if (!ValidateEmailDomain(email))
        {
            return (false, InvalidOrExpiredCodeMessage);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);
        if (user == null)
        {
            return (false, InvalidOrExpiredCodeMessage);
        }

        var emailCode = await _db.EmailCodes
            .Where(ec => ec.Email != null &&
                         ec.Email.ToLower() == email &&
                         ec.Code == request.Code &&
                         ec.Purpose == PasswordResetCodePurpose &&
                         ec.IsUsed == "0")
            .OrderByDescending(ec => ec.SendTime)
            .FirstOrDefaultAsync();

        if (emailCode == null)
        {
            return (false, InvalidOrExpiredCodeMessage);
        }

        if (emailCode.ExpireTime < DateTime.Now)
        {
            emailCode.IsUsed = "1";
            await _db.SaveChangesAsync();
            return (false, "验证码已过期");
        }

        if (!ValidatePassword(request.NewPassword))
        {
            return (false, "密码必须包含大小写字母和数字");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        emailCode.IsUsed = "1";
        await MarkUnusedCodesAsUsedAsync(email, PasswordResetCodePurpose);
        
        await _db.SaveChangesAsync();

        _logger.LogInformation("用户密码重置成功: {Email}", email);
        return (true, "密码重置成功");
    }

    private async Task MarkUnusedCodesAsUsedAsync(string email, string purpose)
    {
        var unusedCodes = await _db.EmailCodes
            .Where(ec => ec.Email != null &&
                         ec.Email.ToLower() == email &&
                         ec.Purpose == purpose &&
                         ec.IsUsed == "0")
            .ToListAsync();

        foreach (var code in unusedCodes)
        {
            code.IsUsed = "1";
        }
    }

    private string GenerateVerificationCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    private string GenerateUserCode()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private string? GetDebugCode(string code)
    {
        return _environment.IsDevelopment() ? code : null;
    }

    private bool ValidatePassword(string password)
    {
        if (password.Length < _authSettings.PasswordMinLength) return false;
        
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        
        return hasUpper && hasLower && hasDigit;
    }
}
