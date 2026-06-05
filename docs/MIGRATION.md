# 数据库迁移指南

## 概述

本次更新对用户权限系统和等级升级系统进行了重大改革。**您需要重新初始化数据库来应用这些更改**。

---

## 修改内容总结

### 1. 新增字段
- `User.userLevel` - 用户等级（1-10）
- `User.totalCredit` - 用户总积分

### 2. 新增角色
- `Manager` - 普通管理员（新增）

### 3. 新增权限
- `dashboard.view` - 查看后台面板
- `admin.add` - 添加管理员
- `admin.delete` - 删除管理员
- `posts.color` - 发表彩色帖子
- `posts.top` - 置顶帖子
- `posts.elite` - 设置精华帖
- `posts.vote` - 创建投票

### 4. 新增表格关系
- Role 表新增 Manager 角色
- Permission 表新增 7 个新权限
- RolePermission 表新增权限分配

---

## 迁移步骤

### 方法 1：完全重建（推荐 - 用于开发环境）

#### 第 1 步：停止运行中的容器
```bash
cd /path/to/project
sudo docker compose down -v
```

#### 第 2 步：删除旧的镜像（可选）
```bash
sudo docker image prune -a
```

#### 第 3 步：重新启动
```bash
sudo docker compose up -d
```

**等待 2-3 分钟** 让所有服务完全启动。

#### 第 4 步：验证

查看 Oracle 是否成功初始化：
```bash
sudo docker compose logs oracle-db | grep "DATABASE IS READY"
```

查看后端是否成功连接：
```bash
sudo docker compose logs backend-api | grep "Application started"
```

---

### 方法 2：只更新应用代码（无需重启数据库）

如果你只想更新后端代码，而保留现有数据：

#### 第 1 步：停止后端容器
```bash
sudo docker compose stop backend-api
```

#### 第 2 步：重新构建后端镜像
```bash
sudo docker compose build --no-cache backend-api
```

#### 第 3 步：启动后端
```bash
sudo docker compose up -d backend-api
```

**注意**：这样做会保留现有用户数据，但新的 `userLevel` 和 `totalCredit` 字段会使用默认值（1 和 0）。

---

## 数据库变更细节

### 01_tables.sql
```sql
-- User 表新增字段
ALTER TABLE "User" ADD (
    "userLevel"   NUMBER DEFAULT 1,
    "totalCredit" NUMBER DEFAULT 0
);
```

### 03_RBAC.sql
**新增角色**：
```sql
INSERT INTO "Role" VALUES ('Manager', '普通管理员，可以审核内容和查看后台', ...);
```

**新增权限**（7 个）：
- dashboard.view
- admin.add
- admin.delete
- posts.color
- posts.top
- posts.elite
- posts.vote

**权限分配**：
- Admin：所有权限
- Manager：dashboard.view + 审核权限
- Moderator：审核权限（无后台访问）
- User：基础权限

### 04_init_role.sql
**新增测试用户**：
```
1@tongji.edu.cn  → Admin   (Lv.10, 2700分)
2@tongji.edu.cn  → Manager (Lv.5, 700分)
3@tongji.edu.cn  → Moderator (Lv.3, 450分)
4@tongji.edu.cn  → User    (Lv.1, 0分)
```

---

## 新增文件

### 后端

1. **Services/CreditService.cs**
   - 处理积分添加
   - 自动等级升级
   - 积分阈值管理

2. **Controllers/DashboardController.cs**
   - 后台信息端点
   - 需要 `dashboard.view` 权限

3. **Controllers/UserController.cs**
   - 用户等级查询
   - 积分管理
   - 用户信息获取

4. **Models/DTOs/CreditDTOs.cs**
   - 积分相关数据模型

### 文档

1. **docs/PERMISSION_SYSTEM.md**
   - 完整的权限系统设计文档

---

## 修改的文件

### 后端

1. **Program.cs**
   - 注册 ICreditService
   - 新增 Dashboard 授权策略

2. **Services/AuthService.cs**
   - 新用户注册时初始化 userLevel 和 totalCredit

3. **Models/Entities.cs**
   - User 类新增两个属性

4. **Controllers/RbacController.cs**
   - AssignRolesToUser 添加权限检查

### 数据库脚本

1. **database/01_tables.sql**
   - User 表新增字段

2. **database/03_RBAC.sql**
   - 新增 Manager 角色
   - 新增 7 个权限
   - 新增权限分配

3. **database/04_init_role.sql**
   - 新增测试用户 Manager 和 User 角色

---

## 测试清单

迁移完成后，请验证以下功能：

### 权限系统

- [ ] Admin 用户可以访问后台
- [ ] Manager 用户可以访问后台
- [ ] User 用户不能访问后台（403）
- [ ] Admin 用户可以添加 Manager
- [ ] Manager 用户不能添加 Admin（403）

### 用户等级系统

- [ ] 新用户注册时默认为 Lv.1，积分为 0
- [ ] Admin 用户显示为 Lv.10，积分为 2700
- [ ] 调用 `/api/user/{userId}/level` 返回正确的等级信息
- [ ] 调用 `/api/user/credit/add` 可以增加积分
- [ ] 积分增加后自动升级（如从 0 加到 100 应升级到 Lv.2）

### API 端点

- [ ] GET `/api/user/{userId}/level` - 返回 200
- [ ] GET `/api/user/{userId}/credit` - 返回 200
- [ ] POST `/api/user/credit/add` - 返回 200（需要 Admin 权限）
- [ ] GET `/api/user/profile` - 返回当前用户信息
- [ ] GET `/api/dashboard/info` - 返回 200（需要 dashboard.view 权限）

### 日志

- [ ] 检查后端日志中是否有权限相关的警告
- [ ] 检查是否正确记录了积分增加操作

---

## 故障排除

### 问题 1：Oracle 容器无法启动

**症状**：`docker compose ps` 中 oracle-db 显示 unhealthy

**解决**：
```bash
sudo docker compose logs oracle-db
# 查看日志了解具体错误
sudo docker compose restart oracle-db
```

### 问题 2：后端无法连接数据库

**症状**：后端启动失败，错误提示连接超时

**解决**：
1. 确保 Oracle 已经完全启动（健康检查通过）
2. 检查连接字符串是否正确
3. 查看后端日志：`sudo docker compose logs backend-api`

### 问题 3：新字段为空或为 0

**症状**：已有用户的 userLevel 显示为 null

**解决**：
- 这是正常的。已有用户的字段会使用 DEFAULT 值（1 和 0）
- 重新启动容器后重新登录，字段应该会正确更新

### 问题 4：权限检查失败

**症状**：Admin 用户也无法访问后台

**解决**：
1. 确保用户已登录
2. 检查 token 中是否包含正确的权限
3. 查看日志是否有权限验证错误

---

## 回滚计划

如果需要回滚到之前的版本：

```bash
# 停止容器并删除数据
sudo docker compose down -v

# 回退 Git 代码
git checkout <之前的提交 hash>

# 重新启动
sudo docker compose up -d
```

---

## 总结

| 步骤 | 操作 | 耗时 |
|------|------|------|
| 1 | 停止并删除容器 | < 1 分钟 |
| 2 | 重新启动 | 2-3 分钟 |
| 3 | 验证 | < 1 分钟 |
| **总计** | | **3-5 分钟** |

---

*最后更新：2026-05-31*
