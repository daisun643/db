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
    private static readonly string[] ProtectedRoleNames = ["Admin", "User"];
    private static readonly string[] PrivilegedRoleNames = ["Admin", "Manager"];

    private readonly AppDbContext _db;
    private readonly ILogger<RbacController> _logger;

    public RbacController(AppDbContext db, ILogger<RbacController> logger)
    {
        _db = db;
        _logger = logger;
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

        if (role == null)
            return NotFound(new { message = "角色不存在" });

        return Ok(ToRoleResponse(role));
    }

    [HttpPost("roles")]
    [RequirePermission("roles.manage")]
    public async Task<ActionResult<RoleResponse>> CreateRole([FromBody] CreateRoleRequest request)
    {
        var roleName = NormalizeName(request.RoleName);
        if (string.IsNullOrWhiteSpace(roleName))
            return BadRequest(new { message = "角色名称不能为空" });

        var roleExists = await _db.Roles.CountAsync(r =>
            r.RoleName != null && r.RoleName.ToLower() == roleName.ToLower()) > 0;
        if (roleExists)
            return BadRequest(new { message = "角色名称已存在" });

        var role = new Role
        {
            RoleName = roleName,
            Description = NormalizeOptionalText(request.Description),
            CreateTime = DateTime.Now
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        _logger.LogInformation("创建角色: {RoleName}", roleName);
        return CreatedAtAction(nameof(GetRoleById), new { id = role.RoleID }, ToRoleResponse(role));
    }

    [HttpPut("roles/{id}")]
    [RequirePermission("roles.manage")]
    public async Task<ActionResult<RoleResponse>> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null)
            return NotFound(new { message = "角色不存在" });

        if (IsProtectedRole(role))
            return BadRequest(new { message = "基础角色不能修改" });

        var roleName = NormalizeName(request.RoleName);
        if (string.IsNullOrWhiteSpace(roleName))
            return BadRequest(new { message = "角色名称不能为空" });

        var nameExists = await _db.Roles.CountAsync(r =>
            r.RoleID != id &&
            r.RoleName != null &&
            r.RoleName.ToLower() == roleName.ToLower()) > 0;
        if (nameExists)
            return BadRequest(new { message = "角色名称已存在" });

        role.RoleName = roleName;
        role.Description = NormalizeOptionalText(request.Description);

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新角色: {RoleId}", id);
        return Ok(ToRoleResponse(role));
    }

    [HttpDelete("roles/{id}")]
    [RequirePermission("roles.manage")]
    public async Task<ActionResult> DeleteRole(int id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null)
            return NotFound(new { message = "角色不存在" });

        if (IsProtectedRole(role))
            return BadRequest(new { message = "基础角色不能删除" });

        var hasUsers = await _db.UserRoles.CountAsync(ur => ur.RoleID == id) > 0;
        if (hasUsers)
            return BadRequest(new { message = "该角色下还有用户，无法删除" });

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();

        _logger.LogInformation("删除角色: {RoleId}", id);
        return Ok(new { message = "删除成功" });
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
        if (permission == null)
            return NotFound(new { message = "权限不存在" });

        return Ok(ToPermissionResponse(permission));
    }

    [HttpPost("permissions")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult<PermissionResponse>> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        var permissionName = NormalizeName(request.PermissionName);
        if (string.IsNullOrWhiteSpace(permissionName))
            return BadRequest(new { message = "权限名称不能为空" });

        var permissionExists = await _db.Permissions.CountAsync(p =>
            p.PermissionName != null && p.PermissionName.ToLower() == permissionName.ToLower()) > 0;
        if (permissionExists)
            return BadRequest(new { message = "权限名称已存在" });

        var permission = new Permission
        {
            PermissionName = permissionName,
            Description = NormalizeOptionalText(request.Description),
            Resource = NormalizeOptionalText(request.Resource),
            Action = NormalizeOptionalText(request.Action)
        };

        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();

        _logger.LogInformation("创建权限: {PermissionName}", permissionName);
        return CreatedAtAction(nameof(GetPermissionById), new { id = permission.PermissionID }, ToPermissionResponse(permission));
    }

    [HttpPut("permissions/{id}")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult<PermissionResponse>> UpdatePermission(int id, [FromBody] UpdatePermissionRequest request)
    {
        var permission = await _db.Permissions.FindAsync(id);
        if (permission == null)
            return NotFound(new { message = "权限不存在" });

        var permissionName = NormalizeName(request.PermissionName);
        if (string.IsNullOrWhiteSpace(permissionName))
            return BadRequest(new { message = "权限名称不能为空" });

        var nameExists = await _db.Permissions.CountAsync(p =>
            p.PermissionID != id &&
            p.PermissionName != null &&
            p.PermissionName.ToLower() == permissionName.ToLower()) > 0;
        if (nameExists)
            return BadRequest(new { message = "权限名称已存在" });

        permission.PermissionName = permissionName;
        permission.Description = NormalizeOptionalText(request.Description);
        permission.Resource = NormalizeOptionalText(request.Resource);
        permission.Action = NormalizeOptionalText(request.Action);

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新权限: {PermissionId}", id);
        return Ok(ToPermissionResponse(permission));
    }

    [HttpDelete("permissions/{id}")]
    [RequirePermission("permissions.manage")]
    public async Task<ActionResult> DeletePermission(int id)
    {
        var permission = await _db.Permissions.FindAsync(id);
        if (permission == null)
            return NotFound(new { message = "权限不存在" });

        var isAssigned = await _db.RolePermissions.CountAsync(rp => rp.PermissionID == id) > 0;
        if (isAssigned)
            return BadRequest(new { message = "该权限已被角色使用，无法删除" });

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

        if (IsProtectedRole(role))
            return BadRequest(new { message = "基础角色权限不能修改" });

        var permissionIds = request.PermissionIds.Distinct().ToList();
        var validPermissionCount = await _db.Permissions.CountAsync(p => permissionIds.Contains(p.PermissionID));
        if (validPermissionCount != permissionIds.Count)
            return BadRequest(new { message = "包含不存在的权限" });

        var existingPermissions = await _db.RolePermissions
            .Where(rp => rp.RoleID == roleId)
            .ToListAsync();
        _db.RolePermissions.RemoveRange(existingPermissions);

        foreach (var permissionId in permissionIds)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleID = roleId,
                PermissionID = permissionId
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("为角色 {RoleId} 分配了 {Count} 个权限", roleId, permissionIds.Count);
        return Ok(new { message = "权限分配成功" });
    }

    [HttpGet("users/{userId}/roles")]
    [RequirePermission("roles.manage", "admin.add", "admin.delete")]
    public async Task<ActionResult<List<RoleResponse>>> GetUserRoles(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var roles = await _db.UserRoles
            .Where(ur => ur.UserID == userId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role)
            .ToListAsync();

        return Ok(roles.Where(role => role != null).Select(role => ToRoleResponse(role!)).ToList());
    }

    [HttpPost("users/{userId}/roles")]
    [RequirePermission("roles.manage", "admin.add")]
    public async Task<ActionResult> AssignRolesToUser(int userId, [FromBody] AssignRolesRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var roleIds = request.RoleIds.Distinct().ToList();
        var roles = await _db.Roles
            .Where(r => roleIds.Contains(r.RoleID))
            .ToListAsync();

        if (roles.Count != roleIds.Count)
            return BadRequest(new { message = "包含不存在的角色" });

        var wantToAssignPrivilegedRole = roles.Any(IsPrivilegedRole);

        if (wantToAssignPrivilegedRole && !HasPermission("admin.add"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "分配管理员角色需要 admin.add 权限" });
        }

        var existingRoles = await _db.UserRoles
            .Where(ur => ur.UserID == userId)
            .ToListAsync();
        _db.UserRoles.RemoveRange(existingRoles);

        foreach (var roleId in roleIds)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserID = userId,
                RoleID = roleId,
                AssignTime = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("为用户 {UserId} 分配了 {Count} 个角色", userId, roleIds.Count);
        return Ok(new { message = "角色分配成功" });
    }

    [HttpDelete("users/{userId}/roles/{roleId}")]
    [RequirePermission("roles.manage", "admin.delete")]
    public async Task<ActionResult> RemoveRoleFromUser(int userId, int roleId)
    {
        var userRole = await _db.UserRoles
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => ur.UserID == userId && ur.RoleID == roleId);

        if (userRole == null)
            return NotFound(new { message = "用户角色关系不存在" });

        if (userRole.Role != null && IsPrivilegedRole(userRole.Role) && !HasPermission("admin.delete"))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "移除管理员角色需要 admin.delete 权限" });

        _db.UserRoles.Remove(userRole);
        await _db.SaveChangesAsync();

        _logger.LogInformation("移除用户 {UserId} 的角色 {RoleId}", userId, roleId);
        return Ok(new { message = "角色移除成功" });
    }

    private static string NormalizeName(string value)
    {
        return value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsProtectedRole(Role role)
    {
        return ProtectedRoleNames.Contains(role.RoleName ?? "", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsPrivilegedRole(Role role)
    {
        return PrivilegedRoleNames.Contains(role.RoleName ?? "", StringComparer.OrdinalIgnoreCase);
    }

    private bool HasPermission(string permission)
    {
        return User.IsInRole("Admin") ||
               User.Claims.Any(c => c.Type == "Permission" &&
                                    string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
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
