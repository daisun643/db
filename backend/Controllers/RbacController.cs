using Backend.Authorization;
using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RbacController : ControllerBase
{
    private readonly AppDbContext _db;

    public RbacController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("roles")]
    [RequirePermission("roles.manage", "permissions.manage")]
    public async Task<ActionResult<List<RoleResponse>>> GetAllRoles()
    {
        var roles = await _db.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .ToListAsync();
        return Ok(roles.Select(ToRoleResponse).ToList());
    }

    [HttpGet("roles/{id}")]
    [RequirePermission("roles.manage", "permissions.manage")]
    public async Task<ActionResult<RoleResponse>> GetRoleById(int id)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.RoleID == id);

        return role == null
            ? NotFound(new { message = "角色不存在" })
            : Ok(ToRoleResponse(role));
    }

    [HttpGet("permissions")]
    [RequirePermission("permissions.manage", "roles.manage")]
    public async Task<ActionResult<List<PermissionResponse>>> GetAllPermissions()
    {
        var permissions = await _db.Permissions.ToListAsync();
        return Ok(permissions.Select(ToPermissionResponse).ToList());
    }

    [HttpGet("permissions/{id}")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult<PermissionResponse>> GetPermissionById(int id)
    {
        var permission = await _db.Permissions.FindAsync(id);
        return permission == null
            ? NotFound(new { message = "权限不存在" })
            : Ok(ToPermissionResponse(permission));
    }

    [HttpGet("users/{userId}/roles")]
    [RequirePermission("roles.manage", "admin.add", "admin.delete")]
    public async Task<ActionResult<List<RoleResponse>>> GetUserRoles(int userId)
    {
        if (await _db.Users.CountAsync(u => u.UserID == userId) == 0)
            return NotFound(new { message = "用户不存在" });

        var roleIds = _db.UserRoles
            .Where(ur => ur.UserID == userId)
            .Select(ur => ur.RoleID);
        var roles = await _db.Roles
            .Where(role => roleIds.Contains(role.RoleID))
            .Include(role => role.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .ToListAsync();

        return Ok(roles.Select(ToRoleResponse).ToList());
    }

    private static RoleResponse ToRoleResponse(Role role)
    {
        return new RoleResponse
        {
            RoleID = role.RoleID,
            RoleName = role.RoleName,
            Description = role.Description,
            CreateTime = role.CreateTime,
            Permissions = role.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => ToPermissionResponse(rp.Permission!))
                .ToList()
        };
    }

    private static PermissionResponse ToPermissionResponse(Permission permission)
    {
        return new PermissionResponse
        {
            PermissionID = permission.PermissionID,
            PermissionName = permission.PermissionName,
            Description = permission.Description,
            Resource = permission.Resource,
            Action = permission.Action
        };
    }
}

public class RoleResponse
{
    public int RoleID { get; set; }
    public string? RoleName { get; set; }
    public string? Description { get; set; }
    public DateTime? CreateTime { get; set; }
    public List<PermissionResponse> Permissions { get; set; } = new();
}

public class PermissionResponse
{
    public int PermissionID { get; set; }
    public string? PermissionName { get; set; }
    public string? Description { get; set; }
    public string? Resource { get; set; }
    public string? Action { get; set; }
}
