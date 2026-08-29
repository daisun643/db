using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Authorization;
using Backend.Services;
using Backend.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmailSettings _emailSettings;

    public UsersController(AppDbContext db, IOptions<EmailSettings> emailSettings)
    {
        _db = db;
        _emailSettings = emailSettings.Value;
    }

    [HttpGet]
    [RequirePermission("users.view")]
    public async Task<ActionResult<List<AdminUserResponse>>> GetAll()
    {
        var users = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.UserID)
            .ToListAsync();

        return Ok(users.Select(MapAdminUser).ToList());
    }

    /// <summary>
    /// 按用户名/邮箱搜索可用用户（仅供指派版主使用：Admin 或版块版主）
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string? keyword)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var canAssignManager = User.IsInRole("Admin") ||
            await _db.ForumManagers.CountAsync(fm => fm.UserID == currentUserId) > 0;
        if (!canAssignManager)
            return Forbid();

        var trimmed = keyword?.Trim() ?? "";
        if (trimmed.Length == 0)
            return Ok(new List<object>());

        var users = await _db.Users
            .Where(u => u.Status == "Active" &&
                ((u.Username != null && u.Username.Contains(trimmed)) ||
                 (u.Email != null && u.Email.Contains(trimmed))))
            .OrderBy(u => u.UserID)
            .Take(10)
            .Select(u => new { u.UserID, u.Username, u.Email })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminUserResponse>> GetById(int id)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var isAdmin = User.IsInRole("Admin");
        
        if (id != currentUserId && !isAdmin)
            return Forbid();

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserID == id);
        return user is null ? NotFound() : Ok(MapAdminUser(user));
    }

    [HttpPost]
    [RequirePermission("users.create")]
    public async Task<ActionResult<AdminUserResponse>> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var email = request.Email.Trim().ToLowerInvariant();
        var username = request.Username.Trim();
        if (!ValidateEmailDomain(email))
            return BadRequest(new { message = $"仅支持 @{_emailSettings.AllowedDomain} 邮箱" });

        if (!ValidatePassword(request.Password))
            return BadRequest(new { message = "密码必须包含大小写字母和数字" });

        var exists = await _db.Users.CountAsync(u => u.Email != null && u.Email.ToLower() == email) > 0;
        if (exists)
            return BadRequest(new { message = "该邮箱已存在" });

        var usernameExists = await _db.Users.CountAsync(u => u.Username != null && u.Username.ToLower() == username.ToLower()) > 0;
        if (usernameExists)
            return BadRequest(new { message = "该用户名已存在" });

        var defaultRoleId = await _db.Roles
            .Where(r => r.RoleName == "User")
            .Select(r => r.RoleID)
            .FirstOrDefaultAsync();
        if (defaultRoleId == 0)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "系统默认角色未配置" });

        var user = new User
        {
            Email = email,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Credit = 100,
            Status = "Active",
            UserCode = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            TotalCredit = 0
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.UserRoles.Add(new UserRole
        {
            UserID = user.UserID,
            RoleID = defaultRoleId,
            AssignTime = DateTime.Now
        });

        await _db.SaveChangesAsync();
        user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.UserID == user.UserID);

        return CreatedAtAction(nameof(GetById), new { id = user.UserID }, MapAdminUser(user));
    }

    private bool ValidateEmailDomain(string email)
    {
        var domain = email.Split('@').LastOrDefault();
        return domain?.Equals(_emailSettings.AllowedDomain, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool ValidatePassword(string password)
    {
        return password.Length >= 8 &&
            password.Any(char.IsUpper) &&
            password.Any(char.IsLower) &&
            password.Any(char.IsDigit);
    }

    private static AdminUserResponse MapAdminUser(User user)
    {
        return new AdminUserResponse
        {
            UserID = user.UserID,
            Username = user.Username ?? "",
            Email = user.Email ?? "",
            UserCode = user.UserCode ?? "",
            Credit = user.Credit ?? 0,
            Status = user.Status ?? "",
            UserLevel = user.UserLevel,
            TotalCredit = user.TotalCredit,
            Roles = user.UserRoles
                .Select(ur => ur.Role?.RoleName)
                .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
                .Cast<string>()
                .ToList()
        };
    }
}
