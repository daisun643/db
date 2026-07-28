-- ============================================================
-- 08_email_code_purpose.sql
-- Distinguish registration and password reset verification codes
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'EmailCode' AND COLUMN_NAME = 'purpose';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "EmailCode" ADD ("purpose" VARCHAR2(30))';
    END IF;
END;
/

UPDATE "EmailCode"
SET "purpose" = 'Legacy'
WHERE "purpose" IS NULL;

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_INDEXES
    WHERE INDEX_NAME = 'idx_emailcode_email_purpose';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX "idx_emailcode_email_purpose" ON "EmailCode"("email", "purpose")';
    END IF;
END;
/

COMMIT;
