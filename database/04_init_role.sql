-- ============================================================
-- 04_data.sql
-- Oracle 18c 测试用户数据插入脚本
-- ============================================================
-- 切换到 appuser 用户执行
ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;
-- 插入测试用户
-- ============================================================
-- 用户1: 1@tongji.edu.cn, 密码: Password1, 角色: Admin, 等级: Lv.10
-- 用户2: 2@tongji.edu.cn, 密码: Password2, 角色: Manager, 等级: Lv.5
-- 用户3: 3@tongji.edu.cn, 密码: Password3, 角色: Moderator, 等级: Lv.3
-- 用户4: 4@tongji.edu.cn, 密码: Password4, 角色: User, 等级: Lv.1
-- 
-- 注意: 密码哈希需要在首次运行后更新为实际的BCrypt哈希值
-- 请运行 scripts/generate_password_hashes.sh 来生成正确的哈希值

-- 用户1 - Admin (密码: Password1)
-- 临时使用占位符,需要替换为实际的BCrypt哈希
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "userLevel", "totalCredit") 
VALUES ('Admin User', '1@tongji.edu.cn', '691a4f28a6bbec2d71f5f25b875cb7dc6a43c9a52222e6730360cc809160cb86', 'ADMIN00001', 1000, 'Active', 10, 2700);

-- 用户2 - Manager (密码: Password2)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "userLevel", "totalCredit") 
VALUES ('Manager User', '2@tongji.edu.cn', '3919a69b8a88fb1afeca8cc4116df26f1a0a3822d39fb42ac10a94dd4ccf71e8', 'MANGER00002', 500, 'Active', 5, 700);

-- 用户3 - Moderator (密码: Password3)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "userLevel", "totalCredit") 
VALUES ('Moderator User', '3@tongji.edu.cn', '00703005cd5d0b56a3fbbc6fe34aef776e16e753ae7be588c2be9ff72989c8e5', 'MODER00003', 300, 'Active', 3, 450);

-- 用户4 - User (密码: Password4)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "userLevel", "totalCredit") 
VALUES ('Normal User', '4@tongji.edu.cn', '7ad6f10a94d2c81b90fafde901244cd35047981607aaa514c384b700168f4125', 'USER000004', 100, 'Active', 1, 0);
-- 分配角色给用户
-- ============================================================
-- 用户1 分配 Admin 角色
INSERT INTO "UserRole" ("userId", "roleId", "assignTime")
SELECT u."userId", r."roleId", SYSTIMESTAMP
FROM "User" u, "Role" r
WHERE u."email" = '1@tongji.edu.cn' AND r."roleName" = 'Admin';

-- 用户2 分配 Manager 角色
INSERT INTO "UserRole" ("userId", "roleId", "assignTime")
SELECT u."userId", r."roleId", SYSTIMESTAMP
FROM "User" u, "Role" r
WHERE u."email" = '2@tongji.edu.cn' AND r."roleName" = 'Manager';

-- 用户3 分配 Moderator 角色
INSERT INTO "UserRole" ("userId", "roleId", "assignTime")
SELECT u."userId", r."roleId", SYSTIMESTAMP
FROM "User" u, "Role" r
WHERE u."email" = '3@tongji.edu.cn' AND r."roleName" = 'Moderator';

-- 用户4 分配 User 角色
INSERT INTO "UserRole" ("userId", "roleId", "assignTime")
SELECT u."userId", r."roleId", SYSTIMESTAMP
FROM "User" u, "Role" r
WHERE u."email" = '4@tongji.edu.cn' AND r."roleName" = 'User';
COMMIT;
