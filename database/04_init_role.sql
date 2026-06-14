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
VALUES ('Admin User', '1@tongji.edu.cn', '$2b$11$3O5hymxk/ksE9gQ/FW8N6.FNTbuwEnfDzDtS/aXSENfmffUm3jjxe', 'ADMIN00001', 1000, 'Active', 10, 2700);

-- 用户2 - Manager (密码: Password2)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "userLevel", "totalCredit") 
VALUES ('Manager User', '2@tongji.edu.cn', '$2b$11$IYaMzf4zTCwDMq8aMWlEoesKqcZWdi0vM6Po8RgwEln9xXn9gjc.C', 'MANGER00002', 500, 'Active', 5, 700);

-- 用户3 - Moderator (密码: Password3)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "userLevel", "totalCredit") 
VALUES ('Moderator User', '3@tongji.edu.cn', '$2b$11$fsSnnoWtn6PnZRFRMTyNz.NiXNR3SfLuCS5aInP7B66.on97Nv7Ri', 'MODER00003', 300, 'Active', 3, 450);

-- 用户4 - User (密码: Password4)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "userLevel", "totalCredit") 
VALUES ('Normal User', '4@tongji.edu.cn', '$2b$11$R.r9c/kvZb8d5rFhyFW6oe8lDnzk9bWjS9MdFrSDuZ6bpRErYGaUO', 'USER000004', 100, 'Active', 1, 0);
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
