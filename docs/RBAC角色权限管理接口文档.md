# RBAC 角色权限管理接口文档

本文档面向其他模块成员，说明当前项目中 RBAC 角色权限管理接口的使用方式，以及业务接口如何复用权限校验。

## 1. 基本约定

- 接口前缀：`/api/rbac`
- 鉴权方式：Cookie 登录态，登录后浏览器会携带 `TongjiForumAuth`
- 未登录：返回 `401`
- 已登录但权限不足：返回 `403`
- 普通业务错误：通常返回 `400`，响应体包含 `message`
- Admin 角色默认视为超级管理员，可以通过权限校验

前端开发环境常用地址：

```text
http://localhost:5173
```

后端接口常用地址：

```text
http://localhost:8080/api/rbac
```

## 2. 权限校验如何复用

后端接口需要权限保护时，引入：

```csharp
using Backend.Authorization;
```

然后在 Controller Action 上添加：

```csharp
[RequirePermission("forums.manage")]
public async Task<ActionResult> CreateSomething()
{
    ...
}
```

如果多个权限任意一个满足即可访问：

```csharp
[RequirePermission("roles.manage", "permissions.manage")]
```

权限校验逻辑：

- 当前用户拥有 `Admin` 角色：直接通过
- 当前用户的 Cookie Claims 中包含任一所需 `Permission`：通过
- 否则返回 `403`

重要：给用户分配角色或修改角色权限后，用户需要重新登录，新的角色和权限才会写入 Cookie Claims。

## 3. 权限命名建议

建议统一使用：

```text
资源.动作
```

示例：

```text
forums.view
forums.manage
posts.audit
products.manage
orders.arbitrate
reports.review
roles.manage
permissions.manage
admin.add
admin.delete
```

其他成员新增后台能力时，应先和负责 RBAC 的成员同步权限名，再在接口上使用 `[RequirePermission("模块.动作")]`。

## 4. 响应结构

### RoleResponse

角色接口返回：

```json
{
  "roleID": 1,
  "roleName": "Admin",
  "description": "系统管理员",
  "createTime": "2026-07-08T12:00:00",
  "permissions": [
    {
      "permissionID": 1,
      "permissionName": "roles.manage",
      "description": "管理角色",
      "resource": "roles",
      "action": "manage"
    }
  ]
}
```

### PermissionResponse

权限接口返回：

```json
{
  "permissionID": 1,
  "permissionName": "roles.manage",
  "description": "管理角色",
  "resource": "roles",
  "action": "manage"
}
```

## 5. 角色接口

### 获取角色列表

```http
GET /api/rbac/roles
```

需要权限：

```text
roles.manage 或 permissions.manage
```

返回：

```json
[
  {
    "roleID": 1,
    "roleName": "Admin",
    "description": "系统管理员",
    "createTime": "2026-07-08T12:00:00",
    "permissions": []
  }
]
```

### 获取单个角色

```http
GET /api/rbac/roles/{id}
```

需要权限：

```text
roles.manage 或 permissions.manage
```

角色不存在返回 `404`。

### 创建角色

```http
POST /api/rbac/roles
Content-Type: application/json
```

需要权限：

```text
roles.manage
```

请求体：

```json
{
  "roleName": "ForumModerator",
  "description": "论坛版主管理角色"
}
```

规则：

- `roleName` 会自动去除首尾空格
- `roleName` 不能为空
- `roleName` 大小写不敏感唯一

成功返回 `201`。

### 修改角色

```http
PUT /api/rbac/roles/{id}
Content-Type: application/json
```

需要权限：

```text
roles.manage
```

请求体：

```json
{
  "roleName": "ForumModerator",
  "description": "论坛版主管理角色"
}
```

规则：

- 不允许修改基础角色 `Admin` / `User`
- 角色名称不能为空
- 角色名称大小写不敏感唯一

### 删除角色

```http
DELETE /api/rbac/roles/{id}
```

需要权限：

```text
roles.manage
```

规则：

- 不允许删除基础角色 `Admin` / `User`
- 不允许删除已经分配给用户的角色

成功返回：

```json
{
  "message": "删除成功"
}
```

## 6. 权限接口

### 获取权限列表

```http
GET /api/rbac/permissions
```

需要权限：

```text
permissions.manage 或 roles.manage
```

### 获取单个权限

```http
GET /api/rbac/permissions/{id}
```

需要权限：

```text
permissions.manage
```

### 创建权限

```http
POST /api/rbac/permissions
Content-Type: application/json
```

需要权限：

```text
permissions.manage
```

请求体：

```json
{
  "permissionName": "forums.manage",
  "description": "管理论坛版块",
  "resource": "forums",
  "action": "manage"
}
```

规则：

- `permissionName` 会自动去除首尾空格
- `permissionName` 不能为空
- `permissionName` 大小写不敏感唯一

成功返回 `201`。

### 修改权限

```http
PUT /api/rbac/permissions/{id}
Content-Type: application/json
```

需要权限：

```text
permissions.manage
```

请求体：

```json
{
  "permissionName": "forums.manage",
  "description": "管理论坛版块",
  "resource": "forums",
  "action": "manage"
}
```

### 删除权限

```http
DELETE /api/rbac/permissions/{id}
```

需要权限：

```text
permissions.manage
```

规则：

- 不允许删除已经分配给角色的权限

成功返回：

```json
{
  "message": "删除成功"
}
```

## 7. 角色权限分配接口

### 给角色分配权限

```http
POST /api/rbac/roles/{roleId}/permissions
Content-Type: application/json
```

需要权限：

```text
permissions.manage
```

请求体：

```json
{
  "permissionIds": [1, 2, 3]
}
```

规则：

- 该接口会覆盖角色原有权限，不是增量追加
- 重复的权限 ID 会自动去重
- 如果包含不存在的权限 ID，返回 `400`
- 不允许修改基础角色 `Admin` / `User` 的权限

成功返回：

```json
{
  "message": "权限分配成功"
}
```

## 8. 用户角色分配接口

### 获取用户角色

```http
GET /api/rbac/users/{userId}/roles
```

需要权限：

```text
roles.manage 或 admin.add 或 admin.delete
```

返回：

```json
[
  {
    "roleID": 3,
    "roleName": "User",
    "description": "普通用户",
    "createTime": "2026-07-08T12:00:00",
    "permissions": []
  }
]
```

### 给用户分配角色

```http
POST /api/rbac/users/{userId}/roles
Content-Type: application/json
```

需要权限：

```text
roles.manage 或 admin.add
```

请求体：

```json
{
  "roleIds": [2, 3]
}
```

规则：

- 该接口会覆盖用户原有角色，不是增量追加
- 重复的角色 ID 会自动去重
- 如果包含不存在的角色 ID，返回 `400`
- 分配 `Admin` / `Manager` 这类管理员角色时，当前操作者必须拥有 `admin.add`
- 被分配角色的用户需要重新登录，新的 Claims 才会生效

成功返回：

```json
{
  "message": "角色分配成功"
}
```

### 移除用户角色

```http
DELETE /api/rbac/users/{userId}/roles/{roleId}
```

需要权限：

```text
roles.manage 或 admin.delete
```

规则：

- 移除 `Admin` / `Manager` 这类管理员角色时，当前操作者必须拥有 `admin.delete`

成功返回：

```json
{
  "message": "角色移除成功"
}
```

## 9. 前端调用示例

前端已经在 `frontend/src/api/index.js` 中封装了常用方法：

```js
getRoles()
createRole(data)
updateRole(id, data)
deleteRole(id)
getPermissions()
createPermission(data)
deletePermission(id)
assignPermissionsToRole(roleId, permissionIds)
getUserRoles(userId)
assignRolesToUser(userId, roleIds)
```

示例：

```js
await assignPermissionsToRole(roleId, [1, 2, 3])
await assignRolesToUser(userId, [2, 3])
```

## 10. 常见错误

### 401 未登录

原因：没有登录 Cookie，或登录态过期。

处理：跳转登录页重新登录。

### 403 权限不足

原因：当前用户没有接口要求的权限，也不是 Admin。

处理：让管理员给用户分配包含对应权限的角色，然后重新登录。

### 400 角色或权限名称已存在

原因：角色名或权限名重复，且唯一性判断不区分大小写。

### 400 基础角色不能修改或删除

原因：`Admin` / `User` 是基础角色，受保护。

### 400 权限或角色正在使用

原因：

- 角色已经分配给用户，不能删除
- 权限已经分配给角色，不能删除

## 11. 对其他成员的接入建议

1. 每个模块先列出自己需要的权限名，例如 `products.manage`、`reports.review`。
2. 后端接口加 `[RequirePermission("权限名")]`。
3. 前端根据 `/api/auth/me` 返回的 `permissions` 控制菜单或按钮显示。
4. 真正的安全边界以后端 `[RequirePermission]` 为准，前端隐藏按钮只是体验优化。
5. 权限或角色调整后，提醒测试账号退出并重新登录。

