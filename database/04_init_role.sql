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
-- 注意: 密码哈希需要在首次运行后更新为实际的BCrypt哈希值
-- 请运行 scripts/generate_password_hashes.sh 来生成正确的哈希值
-- 用户1 - Admin (密码: Password1)
-- 临时使用占位符,需要替换为实际的BCrypt哈希
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status") 
VALUES ('Admin User', '1@tongji.edu.cn', '$2b$11$3O5hymxk/ksE9gQ/FW8N6.FNTbuwEnfDzDtS/aXSENfmffUm3jjxe', 'ADMIN00001', 1000, 'Active');
-- 用户2 - Moderator (密码: Password2)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status") 
VALUES ('Moderator User', '2@tongji.edu.cn', '$2b$11$IYaMzf4zTCwDMq8aMWlEoesKqcZWdi0vM6Po8RgwEln9xXn9gjc.C', 'MODER00002', 500, 'Active');
-- 用户3 - User (密码: Password3)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status") 
VALUES ('Normal User', '3@tongji.edu.cn', '$2b$11$fsSnnoWtn6PnZRFRMTyNz.NiXNR3SfLuCS5aInP7B66.on97Nv7Ri', 'USER000003', 100, 'Active');
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
