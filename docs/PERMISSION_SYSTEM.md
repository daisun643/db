# 权限系统与用户等级升级系统设计文档

## 概述

本文档描述了同济论坛的完整权限管理体系，包括：
1. **三级管理员体系** - Admin（主管理员）、Manager（普通管理员）、普通用户
2. **用户等级升级系统** - Lv.1 到 Lv.10，通过积分升级
3. **权限控制系统** - 基于角色和权限的细粒度控制

---

## 管理员体系

### 三个管理员角色

| 角色 | 权限 | 能做什么 |
|------|------|--------|
| **Admin（主管理员）** | 所有权限 | 添加/删除管理员、查看后台、管理所有内容 |
| **Manager（普通管理员）** | 受限权限 | 查看后台、审核内容、但不能添加管理员 |
| **Moderator（版主）** | 审核权限 | 审核帖子、管理论坛、但不能查看后台 |
| **User（普通用户）** | 基础权限 | 发帖、评论、交易 |

### 关键权限说明

```
✅ dashboard.view      → 查看后台面板（主管理员、普通管理员）
✅ admin.add           → 添加管理员（仅主管理员）
✅ admin.delete        → 删除管理员（仅主管理员）
✅ roles.manage        → 管理角色（仅主管理员）
✅ permissions.manage  → 管理权限（仅主管理员）
✅ users.view          → 查看用户列表（管理员/版主）
✅ users.ban           → 封禁用户（管理员/版主）
✅ posts.moderate      → 审核帖子（管理员/版主）
```

### 实现细节

#### 1. 添加管理员权限检查

在 `RbacController.AssignRolesToUser()` 中：
```csharp
// 分配管理员角色时检查 admin.add 权限
if (wantToAssignAdminRole && !userPermissions.Contains("admin.add"))
{
    return Forbid("您没有添加管理员的权限");
}
```

#### 2. 后台访问权限检查

创建 `DashboardController`，要求 `dashboard.view` 权限：
```csharp
[Authorize(Policy = "Dashboard")]
public class DashboardController : ControllerBase
```

---

## 用户等级升级系统

### 等级定义（Lv.1 - Lv.10）

| 等级 | 所需积分 | 权限 |
|------|---------|------|
| Lv.1 | 0-99 | 基础发帖、评论 |
| Lv.2 | 100-149 | 基础 + 可见高级功能 |
| Lv.3 | 150-199 | 基础 + **发彩色帖子** |
| Lv.4 | 200-249 | 基础 + 彩色帖子 |
| Lv.5 | 250-449 | 基础 + 彩色 + **置顶帖子** |
| Lv.6 | 450-699 | 基础 + 彩色 + 置顶 + **设置精华** |
| Lv.7 | 700-1199 | 基础 + 彩色 + 置顶 + 精华 + **创建投票** |
| Lv.8 | 1200-1699 | 基础 + ... |
| Lv.9 | 1700-2199 | 基础 + ... |
| Lv.10 | 2700+ | VIP 会员，所有权限 |

### 积分阈值

每个等级所需的**累计积分**：

```
Lv.1  → Lv.2:  需要 100 积分
Lv.2  → Lv.3:  需要 150 积分（累计 250）
Lv.3  → Lv.4:  需要 200 积分（累计 450）
Lv.4  → Lv.5:  需要 250 积分（累计 700）
Lv.5  → Lv.6:  需要 300 积分（累计 1000）
Lv.6  → Lv.7:  需要 350 积分（累计 1350）
Lv.7  → Lv.8:  需要 400 积分（累计 1750）
Lv.8  → Lv.9:  需要 450 积分（累计 2200）
Lv.9  → Lv.10: 需要 500 积分（累计 2700）
```

### 积分获取方式

| 行为 | 积分 |
|------|------|
| 发布帖子 | +10 |
| 发表评论 | +5 |
| 帖子被赞 | +2/个 |
| 完成任务 | +20-50 |
| 邀请新用户 | +15 |

### 等级权限对应

高级等级权限存储在 `Permission` 表中：

```sql
posts.color   → Lv.3+ 发表彩色帖子
posts.top     → Lv.5+ 置顶帖子
posts.elite   → Lv.6+ 设置精华帖
posts.vote    → Lv.7+ 创建投票
```

---

## 数据库表结构

### User 表新字段

```sql
ALTER TABLE "User" ADD (
    "userLevel"   NUMBER DEFAULT 1,      -- 用户当前等级 (1-10)
    "totalCredit" NUMBER DEFAULT 0       -- 用户总积分
);
```

### 现有字段

- `credit` - 保留原有的积分字段（用于其他功能）
- `userLevel` - 用户等级（新增）
- `totalCredit` - 用户总积分（新增）

---

## API 端点

### 用户等级相关

#### 获取用户等级信息
```
GET /api/user/{userId}/level
Response:
{
    "userId": 1,
    "currentLevel": 5,
    "totalCredit": 700,
    "nextLevelRequirement": 300,
    "creditToNextLevel": 300
}
```

#### 获取用户积分详情
```
GET /api/user/{userId}/credit
Response:
{
    "userId": 1,
    "username": "User Name",
    "email": "user@tongji.edu.cn",
    "userLevel": 5,
    "totalCredit": 700,
    "credit": 100
}
```

#### 添加积分（管理员）
```
POST /api/user/credit/add
Body:
{
    "userId": 1,
    "credit": 10,
    "reason": "发布帖子"
}
Response: { "message": "积分添加成功" }
```

#### 获取当前用户信息
```
GET /api/user/profile
Response:
{
    "userId": 1,
    "username": "Admin User",
    "email": "1@tongji.edu.cn",
    "userLevel": 10,
    "totalCredit": 2700,
    "credit": 1000,
    "status": "Active",
    "roles": [...],
    "permissions": [...]
}
```

### 后台相关

#### 查看仪表板
```
GET /api/dashboard/info
Requires: dashboard.view permission
```

#### 分配管理员角色
```
POST /api/rbac/users/{userId}/roles
Requires: admin.add permission
Body:
{
    "roleIds": [1]  // 1 = Admin 角色 ID
}
```

---

## 权限检查流程

### 1. 访问后台

```
用户请求 → 检查是否有 dashboard.view 权限
     ├─ 有 → 允许访问
     └─ 无 → 返回 403 Forbidden
```

### 2. 分配管理员

```
用户请求分配管理员角色 → 检查是否要分配 Admin/Manager 角色
     ├─ 是 → 检查用户是否有 admin.add 权限
     │        ├─ 有 → 允许分配
     │        └─ 无 → 返回 403 Forbidden
     └─ 否 → 允许分配
```

### 3. 用户升级

```
用户行为（发帖、评论等） → 调用 CreditService.AddCreditAsync()
     → 自动计算新等级 → 等级变化时自动升级
     → 返回新的 userLevel 和 totalCredit
```

---

## 后端服务

### CreditService

处理积分和等级相关的业务逻辑：

```csharp
public interface ICreditService
{
    // 添加积分并检查升级
    Task AddCreditAsync(int userId, int credit, string reason);
    
    // 获取用户当前等级
    Task<int> GetUserLevelAsync(int userId);
    
    // 获取升级所需积分
    int GetLevelUpRequirement(int currentLevel);
    
    // 获取用户总积分
    Task<int> GetUserTotalCreditAsync(int userId);
}
```

### 使用示例

```csharp
// 在发布帖子时调用
await _creditService.AddCreditAsync(userId, 10, "发布帖子");

// 在评论时调用
await _creditService.AddCreditAsync(userId, 5, "发表评论");

// 当帖子被赞时调用
await _creditService.AddCreditAsync(postAuthorId, 2, "帖子被赞");
```

---

## 测试用户

| 邮箱 | 密码 | 角色 | 等级 | 总积分 |
|------|------|------|------|--------|
| 1@tongji.edu.cn | Password1 | Admin | Lv.10 | 2700 |
| 2@tongji.edu.cn | Password2 | Manager | Lv.5 | 700 |
| 3@tongji.edu.cn | Password3 | Moderator | Lv.3 | 450 |
| 4@tongji.edu.cn | Password4 | User | Lv.1 | 0 |

---

## 注意事项

1. **数据库迁移**：由于修改了 User 表，需要重新运行数据库初始化脚本
2. **权限缓存**：登录后的权限会缓存在 JWT token 中，修改权限后需要重新登录
3. **积分溯源**：所有积分添加操作都会被记录在日志中
4. **等级自动升级**：添加积分时会自动计算并升级用户等级

---

## 后续可扩展功能

1. **积分日志表**：记录每次积分变化
2. **荣誉勋章系统**：特殊成就的徽章
3. **用户封禁系统**：违规扣分或永久封禁
4. **VIP 等级**：Lv.10+ 的差异化定价
5. **积分兑换**：积分换取实物或虚拟物品

---

