using Backend.Models.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
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

        await SignInUserAsync(user.UserID, user.Email!, user.Username!, new List<string> { "User" }, new List<string>());

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
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "未登录"
            });
        }

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "获取成功",
            User = new UserInfo
            {
                UserId = int.Parse(userId),
                Username = username ?? "",
                Email = email ?? "",
                Credit = 0,
                Status = "Active"
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

        var hasAccess = request.Path switch
        {
            "/system-status" => User.IsInRole("Admin"),
            "/" => true,
            "/forums" => true,
            "/products" => true,
            _ => true
        };

        return Ok(new { hasAccess });
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
}
