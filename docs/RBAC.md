# 认证与 RBAC

## 基本约定

- 登录态由后端签发的 `TongjiForumAuth` Cookie 保存。
- 后端是权限判断的唯一可信边界；前端菜单和路由守卫只改善体验。
- 敏感接口必须使用 `[Authorize]`、权限声明和资源所有权检查，不能只判断前端角色。
- 登录后角色或权限发生变化，需要重新登录以刷新 Cookie Claims。

## 数据模型

```text
User ─ UserRole ─ Role ─ RolePermission ─ Permission
```

预置角色包括 `Admin`、`Manager`、`Moderator`、`User`。权限使用稳定的字符串编码，例如：

- `dashboard.view`
- `users.view`、`users.edit`、`users.ban`
- `forums.create`、`forums.edit`
- `posts.create`、`posts.moderate`、`posts.delete`
- `products.create`、`products.edit`、`products.delete`
- `roles.manage`、`permissions.manage`

完整权限集合与预置分配以 `database/03_RBAC.sql`、`database/04_init_role.sql` 为准。

## 接口

路由访问检查：

```http
POST /api/auth/check-route-access
Content-Type: application/json

{ "path": "/system-status" }
```

RBAC 管理接口位于 `/api/rbac`：

```text
GET/POST          /roles
GET/PUT/DELETE    /roles/{id}
GET/POST          /permissions
GET/PUT/DELETE    /permissions/{id}
POST              /roles/{roleId}/permissions
GET/POST          /users/{userId}/roles
DELETE            /users/{userId}/roles/{roleId}
```

请求模型、响应字段和状态码以 Swagger 为准。

## 后端接入

优先使用项目的权限特性或 Claims 权限检查：

```csharp
[Authorize]
public async Task<IActionResult> Update(int id)
{
    var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var allowed = User.IsInRole("Admin") ||
        User.Claims.Any(c => c.Type == "Permission" && c.Value == "posts.edit");
    // 仍需检查资源是否属于当前用户或其管理范围。
}
```

新增受限能力时：

1. 在数据库脚本中定义权限编码和角色分配；
2. 在后端接口执行权限及资源范围检查；
3. 添加 401、403、授权成功和越权资源测试；
4. 如涉及前端入口，再配置路由检查和菜单展示。

不要依赖角色 ID、前端隐藏、请求体中的用户 ID，或未经签名的客户端状态进行授权。
