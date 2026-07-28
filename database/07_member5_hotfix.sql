-- ============================================================
-- 成员五热修复：审核队列与敏感词内容回填
-- 适用于已经初始化过 Oracle 数据卷的项目。
-- 在 sqlplus 中以 appuser 连接 XEPDB1 后执行。
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

-- 1. 旧库没有 targetType 时补列。新增敏感词帖子/评论的审核记录依赖该字段。
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'AuditRecord'
      AND COLUMN_NAME = 'targetType';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "AuditRecord" ADD ("targetType" VARCHAR2(50) DEFAULT ''Post'')';
    END IF;
END;
/

UPDATE "AuditRecord"
SET "targetType" = 'Post'
WHERE "targetType" IS NULL;

-- 2. 修复旧版本留下的 PendingReview 内容：此前帖子/评论已写入，但审核记录插入失败。
INSERT INTO "AuditRecord" ("targetType", "targetId", "triggerWord", "status", "createTime")
SELECT 'Post', p."postId", '历史待审核内容', 'Pending', COALESCE(p."updateTime", p."createTime")
FROM "Post" p
WHERE p."status" = 'PendingReview'
  AND NOT EXISTS (
      SELECT 1
      FROM "AuditRecord" a
      WHERE a."targetType" = 'Post'
        AND a."targetId" = p."postId"
        AND a."status" = 'Pending'
  );

INSERT INTO "AuditRecord" ("targetType", "targetId", "triggerWord", "status", "createTime")
SELECT 'Comment', c."commentId", '历史待审核内容', 'Pending', c."createTime"
FROM "PostComment" c
WHERE c."status" = 'PendingReview'
  AND NOT EXISTS (
      SELECT 1
      FROM "AuditRecord" a
      WHERE a."targetType" = 'Comment'
        AND a."targetId" = c."commentId"
        AND a."status" = 'Pending'
  );

COMMIT;
