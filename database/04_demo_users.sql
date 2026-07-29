-- ============================================================
-- 04_demo_users.sql
-- Oracle 18c 本地演示账号与用户角色关系
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
-- 密码哈希使用 BCrypt 生成；如需预置新用户，可运行:
-- python scripts/encrypt.py "YourPassword123"

-- 用户1 - Admin (密码: Password1)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "totalCredit")
VALUES ('Admin User', '1@tongji.edu.cn', '$2b$11$qhCJLT7aCwPGasI0QVh/hOjWvtYwHfEJb.5bOwVo1Hsi362Kq4kHy', 'ADMIN00001', 1000, 'Active', 2700);

-- 用户2 - Manager (密码: Password2)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "totalCredit")
VALUES ('Manager User', '2@tongji.edu.cn', '$2b$11$apixksR2vP7HoAQw2YXt.eMoY4CLX6CXKnfIossSDVAgwQ/C84Rq.', 'MANGER00002', 500, 'Active', 700);

-- 用户3 - Moderator (密码: Password3)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "totalCredit")
VALUES ('Moderator User', '3@tongji.edu.cn', '$2b$11$LBh/YZ8C.6g82E./7Rsoe.jja7rVgIENjVtOqVPdELUMc6LFHB35W', 'MODER00003', 300, 'Active', 450);

-- 用户4 - User (密码: Password4)
INSERT INTO "User" ("username", "email", "passwordHash", "userCode", "credit", "status", "totalCredit")
VALUES ('Normal User', '4@tongji.edu.cn', '$2b$11$alUNRE/Pr407uL92P4XaOOzLFDdvGlw4S.KwikGwMOPH1WNrBEVdy', 'USER000004', 100, 'Active', 0);
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
