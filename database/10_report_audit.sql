-- ============================================================
-- 10_report_audit.sql
-- 成员 9：举报与审核闭环 —— 增量变更
-- 1. ReportTicket 增加补充说明字段 description
-- 2. 举报查重索引（reporterId + targetType + targetId）
-- 幂等：重复执行不报错（列已存在 / 索引已存在时跳过）
-- 注意：既有数据卷需手动执行一次（见 docker exec sqlplus 命令）
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

-- 1. 补充说明字段
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE "ReportTicket" ADD "description" VARCHAR2(1000)';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -1430 AND SQLCODE != -2260 THEN
            RAISE;
        END IF;
END;
/

-- 2. 举报查重索引（同一用户对同一对象的历史举报）
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX "idx_report_reporter_target" ON "ReportTicket"("reporterId", "targetType", "targetId")';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/
