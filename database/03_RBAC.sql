-- ============================================================
-- 03_data.sql
-- Oracle 18c 数据库初始数据插入脚本
-- ============================================================

-- 切换到 appuser 用户执行
ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

-- 插入默认角色
-- ============================================================

INSERT INTO "Role" ("roleName", "description", "createTime") VALUES ('Admin', '系统管理员，拥有所有权限', SYSTIMESTAMP);
INSERT INTO "Role" ("roleName", "description", "createTime") VALUES ('Moderator', '版主，可以管理论坛和审核内容', SYSTIMESTAMP);
INSERT INTO "Role" ("roleName", "description", "createTime") VALUES ('User', '普通用户，基础权限', SYSTIMESTAMP);

-- 插入权限 - 用户管理
-- ============================================================

INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('users.view', '查看用户列表', 'users', 'view');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('users.create', '创建用户', 'users', 'create');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('users.edit', '编辑用户信息', 'users', 'edit');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('users.delete', '删除用户', 'users', 'delete');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('users.ban', '封禁用户', 'users', 'ban');

-- 插入权限 - 论坛管理
-- ============================================================

INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('forums.view', '查看论坛', 'forums', 'view');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('forums.create', '创建论坛', 'forums', 'create');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('forums.edit', '编辑论坛', 'forums', 'edit');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('forums.delete', '删除论坛', 'forums', 'delete');

-- 插入权限 - 帖子管理
-- ============================================================

INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('posts.view', '查看帖子', 'posts', 'view');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('posts.create', '发布帖子', 'posts', 'create');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('posts.edit', '编辑帖子', 'posts', 'edit');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('posts.delete', '删除帖子', 'posts', 'delete');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('posts.moderate', '审核帖子', 'posts', 'moderate');

-- 插入权限 - 商品管理
-- ============================================================

INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('products.view', '查看商品', 'products', 'view');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('products.create', '发布商品', 'products', 'create');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('products.edit', '编辑商品', 'products', 'edit');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('products.delete', '删除商品', 'products', 'delete');

-- 插入权限 - 角色和权限管理
-- ============================================================

INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('roles.manage', '管理角色', 'roles', 'manage');
INSERT INTO "Permission" ("permissionName", "description", "resource", "action") VALUES ('permissions.manage', '管理权限', 'permissions', 'manage');

-- 分配权限给角色 - Admin拥有所有权限
-- ============================================================

INSERT INTO "RolePermission" ("roleId", "permissionId") 
SELECT r."roleId", p."permissionId" 
FROM "Role" r, "Permission" p 
WHERE r."roleName" = 'Admin';

-- 分配权限给角色 - Moderator
-- ============================================================

INSERT INTO "RolePermission" ("roleId", "permissionId")
SELECT r."roleId", p."permissionId"
FROM "Role" r, "Permission" p
WHERE r."roleName" = 'Moderator'
AND p."permissionName" IN (
    'users.view', 'users.ban',
    'forums.view', 'forums.edit',
    'posts.view', 'posts.edit', 'posts.delete', 'posts.moderate',
    'products.view'
);

-- 分配权限给角色 - User
-- ============================================================

INSERT INTO "RolePermission" ("roleId", "permissionId")
SELECT r."roleId", p."permissionId"
FROM "Role" r, "Permission" p
WHERE r."roleName" = 'User'
AND p."permissionName" IN (
    'forums.view',
    'posts.view', 'posts.create', 'posts.edit',
    'products.view', 'products.create', 'products.edit'
);

COMMIT;
