 # RBAC 权限控制系统

## 设计原则

本系统采用**后端主导的权限验证**策略，前端不包含任何角色判断逻辑，所有权限决策由后端完成。

## 核心特性

### 1. 前端零权限逻辑
- 前端代码中不包含角色名称（Admin/Moderator/User）
- 前端不存储用户的角色和权限信息
- 所有权限判断通过调用后端API完成
- 用户无法通过浏览器开发者工具篡改权限

### 2. 后端完整验证
- 用户登录时，角色和权限存储在服务端 Cookie/JWT Claims 中
- 每个需要权限的路由访问前，前端调用后端验证接口
- 后端根据 Claims 判断用户是否有权限访问
- 所有敏感API接口都有 `[Authorize]` 特性保护

### 3. 路由级权限控制
- 前端路由守卫调用 `POST /api/auth/check-route-access` 验证权限
- 后端返回 `{ hasAccess: boolean }`
- 前端根据结果决定是否允许访问
- 权限结果会被缓存，避免重复API调用

## 系统架构

```
用户登录
  ↓
后端验证密码
  ↓
生成 Cookie (包含 Claims: userId, roles, permissions)
  ↓
前端存储用户基本信息 (不含 roles/permissions)
  ↓
用户访问受限路由 (/system-status)
  ↓
路由守卫调用后端 API: checkRouteAccess("/system-status")
  ↓
后端检查 Cookie 中的 Claims
  ↓
返回 { hasAccess: true/false }
  ↓
前端允许/拒绝访问
```

## 角色和权限

### 预设角色

| 角色 | 描述 | 权限数量 |
|------|------|----------|
| Admin | 系统管理员 | 全部(22个) |
| Moderator | 版主 | 10个 |
| User | 普通用户 | 7个 |

### 权限分类

- **用户管理**: users.view, users.create, users.edit, users.delete, users.ban
- **论坛管理**: forums.view, forums.create, forums.edit, forums.delete
- **帖子管理**: posts.view, posts.create, posts.edit, posts.delete, posts.moderate
- **商品管理**: products.view, products.create, products.edit, products.delete
- **系统管理**: roles.manage, permissions.manage

## 关键接口

### 后端接口

**权限验证接口**
```
POST /api/auth/check-route-access
Body: { path: "/system-status" }
Response: { hasAccess: true }
```

**RBAC管理接口** (需要Admin权限)
```
GET    /api/rbac/roles              - 获取所有角色
POST   /api/rbac/roles              - 创建角色
GET    /api/rbac/permissions        - 获取所有权限
POST   /api/rbac/users/{id}/roles   - 为用户分配角色
```

### 前端实现

**路由守卫**
```javascript
router.beforeEach(async (to, from, next) => {
  if (to.meta.requiresBackendCheck) {
    const hasAccess = await authStore.checkRouteAccess(to.path)
    if (hasAccess) {
      next()
    } else {
      next('/') // 无权限，重定向
    }
  }
})
```

**权限缓存**
```javascript
// Store 中缓存权限验证结果
routeAccessCache: {
  '/system-status': true,
  '/admin': false
}
```

## 安全保障

### 多层防护

1. **前端隐藏** - 导航栏显示所有菜单，但无权限路由会被拦截
2. **路由守卫** - 访问受限路由时调用后端验证
3. **后端验证** - 所有API接口都有权限检查
4. **资源所有权** - 用户只能操作自己的资源

### 攻击防御

- ✅ 前端代码不包含角色信息，无法推断权限逻辑
- ✅ 用户无法通过修改前端代码绕过权限检查
- ✅ 即使用户猜测URL直接访问，后端也会拒绝
- ✅ Cookie/JWT 由服务端签名，客户端无法伪造

## 数据库结构

```
User → UserRole → Role → RolePermission → Permission
```

- **User**: 用户基本信息
- **Role**: 角色定义
- **Permission**: 权限定义
- **UserRole**: 用户-角色关联（多对多）
- **RolePermission**: 角色-权限关联（多对多）

## 扩展性

系统支持：
- 动态添加新角色和权限
- 灵活的角色权限分配
- 用户可拥有多个角色
- 权限通过角色继承

## 最佳实践

1. **新用户注册** - 自动分配 User 角色
2. **权限提升** - 由管理员通过 RBAC API 手动分配
3. **权限缓存** - 前端缓存验证结果，减少API调用
4. **登出清理** - 登出时清除所有缓存的权限信息
