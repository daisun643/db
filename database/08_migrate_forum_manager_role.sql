-- ============================================================
-- 08. 迁移：ForumManager 增加管理人员角色（版主/管理员）
-- 说明：
--   1. ForumManager 新增 role 列：Moderator=版主，Admin=管理员
--   2. 存量管理人员默认补为版主，保持既有权限不变
--   3. 版块必须有版主：创建者补齐为版主
-- 本脚本幂等，可在全新数据卷（初始化脚本之后）或已部署数据卷上执行。
-- ============================================================

-- Oracle 容器会为每个初始化脚本创建独立会话，必须显式切换到业务 PDB 和 schema。
ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

SET DEFINE OFF

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM all_tab_columns
    WHERE owner = 'APPUSER'
      AND table_name = 'ForumManager'
      AND column_name = 'role';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "ForumManager" ADD ("role" VARCHAR2(20) DEFAULT ''Moderator'' NOT NULL)';
        EXECUTE IMMEDIATE 'ALTER TABLE "ForumManager" ADD CONSTRAINT "CK_ForumMgr_Role" CHECK ("role" IN (''Moderator'', ''Admin''))';
    END IF;
END;
/

UPDATE "ForumManager" SET "role" = 'Moderator' WHERE "role" IS NULL OR "role" NOT IN ('Moderator', 'Admin');

INSERT INTO "ForumManager" ("forumId", "userId", "role")
SELECT f."forumId", f."creatorId", 'Moderator' FROM "Forum" f
WHERE f."creatorId" IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM "ForumManager" fm WHERE fm."forumId" = f."forumId" AND fm."userId" = f."creatorId");

COMMIT;
