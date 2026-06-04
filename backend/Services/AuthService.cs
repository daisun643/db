using Backend.Configuration;
using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BCrypt.Net;

namespace Backend.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;
    private readonly AuthSettings _authSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        IEmailService emailService,
        IOptions<EmailSettings> emailSettings,
        IOptions<AuthSettings> authSettings,
        ILogger<AuthService> logger)
    {
        _db = db;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
        _authSettings = authSettings.Value;
        _logger = logger;
    }

    public bool ValidateEmailDomain(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        
        var domain = email.Split('@').LastOrDefault();
        return domain?.Equals(_emailSettings.AllowedDomain, StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<(bool Success, string Message)> SendVerificationCodeAsync(string email)
    {
        if (!ValidateEmailDomain(email))
        {
            return (false, $"仅支持 @{_emailSettings.AllowedDomain} 邮箱注册");
        }

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existingUser != null)
        {
            return (false, "该邮箱已被注册");
        }

        var code = GenerateVerificationCode();
        var expireTime = DateTime.Now.AddMinutes(_authSettings.CodeExpirationMinutes);

        var emailCode = new EmailCode
        {
            Email = email,
            Code = code,
            SendTime = DateTime.Now,
            ExpireTime = expireTime,
            IsUsed = "0"
        };

        _db.EmailCodes.Add(emailCode);
        await _db.SaveChangesAsync();

        var sent = await _emailService.SendVerificationCodeAsync(email, code);
        if (!sent)
        {
            return (false, "验证码发送失败，请稍后重试");
        }

        return (true, "验证码已发送到您的邮箱");
    }

    public async Task<(bool Success, string Message, User? User)> RegisterAsync(RegisterRequest request)
    {
        if (!ValidateEmailDomain(request.Email))
        {
            return (false, $"仅支持 @{_emailSettings.AllowedDomain} 邮箱注册", null);
        }

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser != null)
        {
            return (false, "该邮箱已被注册", null);
        }

        var emailCode = await _db.EmailCodes
            .Where(ec => ec.Email == request.Email && ec.Code == request.Code && ec.IsUsed == "0")
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
            Email = request.Email,
            Username = request.Username,
            PasswordHash = passwordHash,
            Credit = 100,
            Status = "Active",
            UserCode = GenerateUserCode(),
            UserLevel = 1,
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
            
            _logger.LogInformation("用户注册成功并分配默认角色: {Email}", request.Email);
        }
        else
        {
            _logger.LogWarning("用户注册成功但未找到默认角色: {Email}", request.Email);
        }

        return (true, "注册成功", user);
    }

    public async Task<(bool Success, string Message, User? User, List<string>? Roles, List<string>? Permissions)> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r!.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
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
            request.Email, string.Join(", ", roles), permissions.Count);
        
        return (true, "登录成功", user, roles, permissions);
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
    {
        if (!ValidateEmailDomain(email))
        {
            return (false, $"仅支持 @{_emailSettings.AllowedDomain} 邮箱");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return (false, "该邮箱未注册");
        }

        var code = GenerateVerificationCode();
        var expireTime = DateTime.Now.AddMinutes(_authSettings.CodeExpirationMinutes);

        var emailCode = new EmailCode
        {
            Email = email,
            Code = code,
            SendTime = DateTime.Now,
            ExpireTime = expireTime,
            IsUsed = "0"
        };

        _db.EmailCodes.Add(emailCode);
        await _db.SaveChangesAsync();

        var sent = await _emailService.SendPasswordResetCodeAsync(email, code);
        if (!sent)
        {
            return (false, "验证码发送失败，请稍后重试");
        }

        return (true, "重置验证码已发送到您的邮箱");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            return (false, "该邮箱未注册");
        }

        var emailCode = await _db.EmailCodes
            .Where(ec => ec.Email == request.Email && ec.Code == request.Code && ec.IsUsed == "0")
            .OrderByDescending(ec => ec.SendTime)
            .FirstOrDefaultAsync();

        if (emailCode == null)
        {
            return (false, "验证码无效");
        }

        if (emailCode.ExpireTime < DateTime.Now)
        {
            return (false, "验证码已过期");
        }

        if (!ValidatePassword(request.NewPassword))
        {
            return (false, "密码必须包含大小写字母和数字");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        emailCode.IsUsed = "1";
        
        await _db.SaveChangesAsync();

        _logger.LogInformation("用户密码重置成功: {Email}", request.Email);
        return (true, "密码重置成功");
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

    private bool ValidatePassword(string password)
    {
        if (password.Length < _authSettings.PasswordMinLength) return false;
        
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        
        return hasUpper && hasLower && hasDigit;
    }
}
