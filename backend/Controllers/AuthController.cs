using Backend.Data;
using Backend.Models.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;
    private readonly ILogger<AuthController> _logger;
    private static readonly Dictionary<string, string[]> RoutePermissionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/system-status"] = ["dashboard.view", "roles.manage", "permissions.manage"],
        ["/forums"] = ["forums.view", "posts.view"],
        ["/products"] = ["products.view"],
        ["/messages"] = [],
        ["/profile"] = [],
        ["/"] = []
    };

    public AuthController(IAuthService authService, AppDbContext context, ILogger<AuthController> logger)
    {
        _authService = authService;
        _context = context;
        _logger = logger;
    }

    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "请求参数无效"
            });
        }

        var (success, message) = await _authService.SendVerificationCodeAsync(request.Email);
        
        return Ok(new AuthResponse
        {
            Success = success,
            Message = message
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "请求参数无效"
            });
        }

        var (success, message, user) = await _authService.RegisterAsync(request);
        
        if (!success || user == null)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = message
            });
        }

        var (roles, permissions) = await GetUserAccessAsync(user.UserID);
        await SignInUserAsync(user.UserID, user.Email!, user.Username!, roles, permissions);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = message,
            User = new UserInfo
            {
                UserId = user.UserID,
                Username = user.Username!,
                Email = user.Email!,
                Credit = user.Credit ?? 0,
                Status = user.Status ?? "Active"
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "请求参数无效"
            });
        }

        var (success, message, user, roles, permissions) = await _authService.LoginAsync(request);
        
        if (!success || user == null)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = message
            });
        }

        await SignInUserAsync(user.UserID, user.Email!, user.Username!, roles ?? new List<string>(), permissions ?? new List<string>());

        return Ok(new AuthResponse
        {
            Success = true,
            Message = message,
            User = new UserInfo
            {
                UserId = user.UserID,
                Username = user.Username!,
                Email = user.Email!,
                Credit = user.Credit ?? 0,
                Status = user.Status ?? "Active"
            }
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        return Ok(new AuthResponse
        {
            Success = true,
            Message = "登出成功"
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "请求参数无效"
            });
        }

        var (success, message) = await _authService.ForgotPasswordAsync(request.Email);
        
        return Ok(new AuthResponse
        {
            Success = success,
            Message = message
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "请求参数无效"
            });
        }

        var (success, message) = await _authService.ResetPasswordAsync(request);
        
        return Ok(new AuthResponse
        {
            Success = success,
            Message = message
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "未登录"
            });
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserID == parsedUserId);

        if (user == null)
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "用户不存在"
            });
        }

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "获取成功",
            User = new UserInfo
            {
                UserId = user.UserID,
                Username = user.Username ?? "",
                Email = user.Email ?? "",
                Credit = user.Credit ?? 0,
                Status = user.Status ?? "Active"
            }
        });
    }

    [HttpPost("check-route-access")]
    [Authorize]
    public IActionResult CheckRouteAccess([FromBody] RouteAccessRequest request)
    {
        if (string.IsNullOrEmpty(request.Path))
        {
            return BadRequest(new { hasAccess = false, message = "路径不能为空" });
        }

        var normalizedPath = NormalizeRoutePath(request.Path);
        var hasAccess = RoutePermissionMap.TryGetValue(normalizedPath, out var requiredPermissions)
            && HasAnyPermission(requiredPermissions);

        return Ok(new { hasAccess, path = normalizedPath });
    }

    private static string NormalizeRoutePath(string path)
    {
        var pathOnly = path.Split('?', '#')[0].Trim();
        if (string.IsNullOrWhiteSpace(pathOnly))
        {
            return "/";
        }

        return pathOnly.Length > 1 ? pathOnly.TrimEnd('/') : pathOnly;
    }

    private bool HasAnyPermission(string[] requiredPermissions)
    {
        if (requiredPermissions.Length == 0)
        {
            return true;
        }

        if (User.IsInRole("Admin"))
        {
            return true;
        }

        var userPermissions = User.Claims
            .Where(c => c.Type == "Permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredPermissions.Any(userPermissions.Contains);
    }

    private async Task SignInUserAsync(int userId, string email, string username, List<string> roles, List<string> permissions)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, username)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("Permission", permission));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }

    private async Task<(List<string> Roles, List<string> Permissions)> GetUserAccessAsync(int userId)
    {
        var userRoles = await _context.UserRoles
            .Include(ur => ur.Role)
            .ThenInclude(r => r!.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .Where(ur => ur.UserID == userId)
            .ToListAsync();

        var roles = userRoles
            .Select(ur => ur.Role?.RoleName)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissions = userRoles
            .SelectMany(ur => ur.Role?.RolePermissions ?? Enumerable.Empty<Backend.Models.RolePermission>())
            .Select(rp => rp.Permission?.PermissionName)
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (roles, permissions);
    }
}
