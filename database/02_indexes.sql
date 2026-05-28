-- ============================================================
-- 02_indexes.sql
-- Oracle 18c 数据库索引创建脚本
-- ============================================================

-- 切换到 appuser 用户执行
ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

-- 唯一索引
-- ============================================================

CREATE UNIQUE INDEX "idx_user_email" ON "User"("email");
CREATE UNIQUE INDEX "idx_role_name" ON "Role"("roleName");
CREATE UNIQUE INDEX "idx_permission_name" ON "Permission"("permissionName");

-- 普通索引
-- ============================================================

CREATE INDEX "idx_emailcode_email" ON "EmailCode"("email");
CREATE INDEX "idx_emailcode_sendtime" ON "EmailCode"("sendTime");

COMMIT;
