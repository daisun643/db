-- ============================================================
-- 09_migrate_single_session.sql
-- 用户单会话版本：新登录使同账号的旧 Cookie 失效（幂等）
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

DECLARE
    column_count NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO column_count
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'User'
       AND COLUMN_NAME = 'sessionVersion';

    IF column_count = 0 THEN
        EXECUTE IMMEDIATE '
            ALTER TABLE "User"
            ADD ("sessionVersion" VARCHAR2(64))';
    END IF;
END;
/

COMMIT;
