-- ============================================================
-- 07_user_profile_fields.sql
-- User profile extension fields, safe to run more than once
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'User' AND COLUMN_NAME = 'nickname';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "User" ADD ("nickname" VARCHAR2(50))';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'User' AND COLUMN_NAME = 'avatarUrl';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "User" ADD ("avatarUrl" VARCHAR2(500))';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'User' AND COLUMN_NAME = 'contact';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "User" ADD ("contact" VARCHAR2(100))';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'User' AND COLUMN_NAME = 'bio';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "User" ADD ("bio" VARCHAR2(500))';
    END IF;
END;
/

