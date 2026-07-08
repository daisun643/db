using Backend.Data;
using Backend.Models;
using Backend.Authorization;
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
    private readonly ILogger<RbacController> _logger;

    public RbacController(AppDbContext db, ILogger<RbacController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("roles")]
    [RequirePermission("roles.manage", "permissions.manage", "dashboard.view")]
    public async Task<ActionResult> GetAllRoles()
    {
        // 不直接返回 EF 实体。Role -> RolePermission -> Role 的双向导航属性会形成 JSON 循环，
        // 从而让 /api/rbac/roles 返回 500。
        var roles = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.RoleID)
            .ToListAsync();

        return Ok(roles.Select(ToRoleResponse).ToList());
    }

    [HttpGet("roles/{id}")]
    [RequirePermission("roles.manage", "permissions.manage")]
    public async Task<ActionResult> GetRoleById(int id)
    {
        var role = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.RoleID == id);

        if (role == null)
            return NotFound(new { message = "角色不存在" });

        return Ok(ToRoleResponse(role));
    }

    private static RoleResponse ToRoleResponse(Role role) => new()
    {
        RoleID = role.RoleID,
        RoleName = role.RoleName ?? string.Empty,
        Description = role.Description,
        CreateTime = role.CreateTime,
        RolePermissions = role.RolePermissions
            .Select(rp => new RolePermissionResponse
            {
                RoleID = rp.RoleID,
                PermissionID = rp.PermissionID,
                PermissionName = rp.Permission?.PermissionName ?? string.Empty,
                Description = rp.Permission?.Description
            })
            .ToList()
    };

    [HttpPost("roles")]
    [RequirePermission("roles.manage")]
    public async Task<ActionResult<Role>> CreateRole([FromBody] CreateRoleRequest request)
    {
        if ((await _db.Roles.CountAsync(r => r.RoleName == request.RoleName)) > 0)
            return BadRequest(new { message = "角色名称已存在" });

        var role = new Role
        {
            RoleName = request.RoleName,
            Description = request.Description,
            CreateTime = DateTime.Now
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        _logger.LogInformation("创建角色: {RoleName}", request.RoleName);
        return CreatedAtAction(nameof(GetRoleById), new { id = role.RoleID }, role);
    }

    [HttpPut("roles/{id}")]
    [RequirePermission("roles.manage")]
    public async Task<ActionResult<Role>> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null)
            return NotFound(new { message = "角色不存在" });

        if (request.RoleName != role.RoleName && 
            (await _db.Roles.CountAsync(r => r.RoleName == request.RoleName)) > 0)
            return BadRequest(new { message = "角色名称已存在" });

        role.RoleName = request.RoleName;
        role.Description = request.Description;

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新角色: {RoleId}", id);
        return Ok(role);
    }

    [HttpDelete("roles/{id}")]
    [RequirePermission("roles.manage")]
    public async Task<ActionResult> DeleteRole(int id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null)
            return NotFound(new { message = "角色不存在" });

        var hasUsers = (await _db.UserRoles.CountAsync(ur => ur.RoleID == id)) > 0;
        if (hasUsers)
            return BadRequest(new { message = "该角色下还有用户，无法删除" });

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();

        _logger.LogInformation("删除角色: {RoleId}", id);
        return Ok(new { message = "删除成功" });
    }

    [HttpGet("permissions")]
    [RequirePermission("permissions.manage", "roles.manage", "dashboard.view")]
    public async Task<ActionResult<List<Permission>>> GetAllPermissions()
    {
        var permissions = await _db.Permissions.ToListAsync();
        return Ok(permissions);
    }

    [HttpGet("permissions/{id}")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult<Permission>> GetPermissionById(int id)
    {
        var permission = await _db.Permissions.FindAsync(id);
        if (permission == null)
            return NotFound(new { message = "权限不存在" });

        return Ok(permission);
    }

    [HttpPost("permissions")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult<Permission>> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        if ((await _db.Permissions.CountAsync(p => p.PermissionName == request.PermissionName)) > 0)
            return BadRequest(new { message = "权限名称已存在" });

        var permission = new Permission
        {
            PermissionName = request.PermissionName,
            Description = request.Description,
            Resource = request.Resource,
            Action = request.Action
        };

        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();

        _logger.LogInformation("创建权限: {PermissionName}", request.PermissionName);
        return CreatedAtAction(nameof(GetPermissionById), new { id = permission.PermissionID }, permission);
    }

    [HttpPut("permissions/{id}")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult<Permission>> UpdatePermission(int id, [FromBody] UpdatePermissionRequest request)
    {
        var permission = await _db.Permissions.FindAsync(id);
        if (permission == null)
            return NotFound(new { message = "权限不存在" });

        if (request.PermissionName != permission.PermissionName && 
            (await _db.Permissions.CountAsync(p => p.PermissionName == request.PermissionName)) > 0)
            return BadRequest(new { message = "权限名称已存在" });

        permission.PermissionName = request.PermissionName;
        permission.Description = request.Description;
        permission.Resource = request.Resource;
        permission.Action = request.Action;

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新权限: {PermissionId}", id);
        return Ok(permission);
    }

    [HttpDelete("permissions/{id}")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult> DeletePermission(int id)
    {
        var permission = await _db.Permissions.FindAsync(id);
        if (permission == null)
            return NotFound(new { message = "权限不存在" });

        _db.Permissions.Remove(permission);
        await _db.SaveChangesAsync();

        _logger.LogInformation("删除权限: {PermissionId}", id);
        return Ok(new { message = "删除成功" });
    }

    [HttpPost("roles/{roleId}/permissions")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult> AssignPermissionsToRole(int roleId, [FromBody] AssignPermissionsRequest request)
    {
        var role = await _db.Roles.FindAsync(roleId);
        if (role == null)
            return NotFound(new { message = "角色不存在" });

        var existingPermissions = await _db.RolePermissions
            .Where(rp => rp.RoleID == roleId)
            .ToListAsync();
        _db.RolePermissions.RemoveRange(existingPermissions);

        foreach (var permissionId in request.PermissionIds)
        {
            var permission = await _db.Permissions.FindAsync(permissionId);
            if (permission != null)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleID = roleId,
                    PermissionID = permissionId
                });
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("为角色 {RoleId} 分配了 {Count} 个权限", roleId, request.PermissionIds.Count);
        return Ok(new { message = "权限分配成功" });
    }

    [HttpGet("users/{userId}/roles")]
    [RequirePermission("roles.manage", "admin.add", "admin.delete", "dashboard.view")]
    public async Task<ActionResult<List<Role>>> GetUserRoles(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var roles = await _db.UserRoles
            .Where(ur => ur.UserID == userId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role)
            .ToListAsync();

        return Ok(roles);
    }

    [HttpPost("users/{userId}/roles")]
    [RequirePermission("roles.manage", "admin.add")]
    public async Task<ActionResult> AssignRolesToUser(int userId, [FromBody] AssignRolesRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        // 检查是否要分配管理员角色
        var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin" || r.RoleName == "Manager");
        var wantToAssignAdminRole = request.RoleIds.Any(roleId => roleId == adminRole?.RoleID);

        if (wantToAssignAdminRole)
        {
            // 检查当前用户是否有 admin.add 权限
            var userPermissions = User.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToList();

            if (!userPermissions.Contains("admin.add"))
            {
                return Forbid("您没有添加管理员的权限，只有主管理员(Admin)可以添加管理员");
            }
        }

        var existingRoles = await _db.UserRoles
            .Where(ur => ur.UserID == userId)
            .ToListAsync();
        _db.UserRoles.RemoveRange(existingRoles);

        foreach (var roleId in request.RoleIds)
        {
            var role = await _db.Roles.FindAsync(roleId);
            if (role != null)
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserID = userId,
                    RoleID = roleId,
                    AssignTime = DateTime.Now
                });
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("为用户 {UserId} 分配了 {Count} 个角色", userId, request.RoleIds.Count);
        return Ok(new { message = "角色分配成功" });
    }

    [HttpDelete("users/{userId}/roles/{roleId}")]
    [RequirePermission("roles.manage", "admin.delete")]
    public async Task<ActionResult> RemoveRoleFromUser(int userId, int roleId)
    {
        var userRole = await _db.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserID == userId && ur.RoleID == roleId);

        if (userRole == null)
            return NotFound(new { message = "用户角色关系不存在" });

        _db.UserRoles.Remove(userRole);
        await _db.SaveChangesAsync();

        _logger.LogInformation("移除用户 {UserId} 的角色 {RoleId}", userId, roleId);
        return Ok(new { message = "角色移除成功" });
    }
}

public class RoleResponse
{
    public int RoleID { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? CreateTime { get; set; }
    public List<RolePermissionResponse> RolePermissions { get; set; } = new();
}

public class RolePermissionResponse
{
    public int RoleID { get; set; }
    public int PermissionID { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateRoleRequest
{
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateRoleRequest
{
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreatePermissionRequest
{
    public string PermissionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Resource { get; set; }
    public string? Action { get; set; }
}

public class UpdatePermissionRequest
{
    public string PermissionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Resource { get; set; }
    public string? Action { get; set; }
}

public class AssignPermissionsRequest
{
    public List<int> PermissionIds { get; set; } = new List<int>();
}

public class AssignRolesRequest
{
    public List<int> RoleIds { get; set; } = new List<int>();
}
