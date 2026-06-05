-- ============================================================
-- 04_data.sql
-- Oracle 18c 测试用户数据插入脚本
-- ============================================================
-- 切换到 appuser 用户执行
ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;
-- 插入测试用户
-- ============================================================
-- 用户1: 1@tongji.edu.cn, 密码: Password1, 角色: Admin
-- 用户2: 2@tongji.edu.cn, 密码: Password2, 角色: Moderator
-- 用户3: 3@tongji.edu.cn, 密码: Password3, 角色: User
-- 
-- 注意: passwordHash 存储 SHA-256("tjuer" || password) 的十六进制摘要
-- 用户1 - Admin (密码: Password1)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status") 
VALUES ('Admin User', '1@tongji.edu.cn', '691a4f28a6bbec2d71f5f25b875cb7dc6a43c9a52222e6730360cc809160cb86', 'ADMIN00001', 1000, 'Active');
-- 用户2 - Moderator (密码: Password2)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status") 
VALUES ('Moderator User', '2@tongji.edu.cn', '3919a69b8a88fb1afeca8cc4116df26f1a0a3822d39fb42ac10a94dd4ccf71e8', 'MODER00002', 500, 'Active');
-- 用户3 - User (密码: Password3)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status") 
VALUES ('Normal User', '3@tongji.edu.cn', '00703005cd5d0b56a3fbbc6fe34aef776e16e753ae7be588c2be9ff72989c8e5', 'USER000003', 100, 'Active');
-- 分配角色给用户
-- ============================================================
-- 用户1 分配 Admin 角色
INSERT INTO "UserRole" ("userId", "roleId", "assignTime")
SELECT u."userId", r."roleId", SYSTIMESTAMP
FROM "User" u, "Role" r
WHERE u."email" = '1@tongji.edu.cn' AND r."roleName" = 'Admin';
-- 用户2 分配 Moderator 角色
INSERT INTO "UserRole" ("userId", "roleId", "assignTime")
SELECT u."userId", r."roleId", SYSTIMESTAMP
FROM "User" u, "Role" r
WHERE u."email" = '2@tongji.edu.cn' AND r."roleName" = 'Moderator';
-- 用户3 分配 User 角色
INSERT INTO "UserRole" ("userId", "roleId", "assignTime")
SELECT u."userId", r."roleId", SYSTIMESTAMP
FROM "User" u, "Role" r
WHERE u."email" = '3@tongji.edu.cn' AND r."roleName" = 'User';
COMMIT;
