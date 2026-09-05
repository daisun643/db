-- ============================================================
-- 08_triggers.sql
-- Oracle 18c 函数与触发器：把跨表不变式、派生字段和内容审核规则下沉到数据库
-- 必须排在种子数据（05~07）之后执行，避免改写演示数据中已有的汇总值
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;
SET DEFINE OFF;
SET SQLBLANKLINES ON;

-- ============================================================
-- 1. 敏感词匹配函数
--    词表来自 "PostSensitiveWord" 表，大小写不敏感子串匹配；
--    命中多个词时返回 id 最小的那个，保证结果稳定可复现。
-- ============================================================

CREATE OR REPLACE FUNCTION "fn_find_sensitive_word"(
    p_title   IN VARCHAR2,
    p_content IN CLOB)
RETURN VARCHAR2
IS
    v_word VARCHAR2(200);
BEGIN
    SELECT MIN("word") KEEP (DENSE_RANK FIRST ORDER BY "id")
      INTO v_word
      FROM "PostSensitiveWord"
     WHERE INSTR(LOWER(NVL(p_title, ' ')), LOWER("word")) > 0
        OR (p_content IS NOT NULL
            AND DBMS_LOB.INSTR(LOWER(p_content), LOWER("word")) > 0);

    RETURN v_word;
END;
/

-- ============================================================
-- 2. 帖子内容审核
--    BEFORE：命中敏感词的帖子强制进入 PendingReview；
--    AFTER ：同步写入 AuditRecord，审核队列不再依赖应用层补记。
--    "content" 是 CLOB，Oracle 不允许出现在 UPDATE OF 子句中（ORA-25006），
--    因此改用 UPDATING() 判定，仅在标题或正文真正被写入时才重新审核，
--    避免版主置顶/封禁等纯状态变更把已审核的帖子打回待审核。
-- ============================================================

CREATE OR REPLACE TRIGGER "TRG_Post_SensitiveStatus"
BEFORE INSERT OR UPDATE ON "Post"
FOR EACH ROW
DECLARE
    v_word VARCHAR2(200);
BEGIN
    IF INSERTING OR UPDATING('title') OR UPDATING('content') THEN
        v_word := "fn_find_sensitive_word"(:NEW."title", :NEW."content");
        IF v_word IS NOT NULL THEN
            :NEW."status" := 'PendingReview';
        END IF;
    END IF;
END;
/

CREATE OR REPLACE TRIGGER "TRG_Post_SensitiveAudit"
AFTER INSERT OR UPDATE ON "Post"
FOR EACH ROW
DECLARE
    v_word VARCHAR2(200);
BEGIN
    IF INSERTING OR UPDATING('title') OR UPDATING('content') THEN
        v_word := "fn_find_sensitive_word"(:NEW."title", :NEW."content");
        IF v_word IS NOT NULL THEN
            INSERT INTO "AuditRecord" ("targetType", "targetId", "triggerWord", "status", "createTime")
            VALUES ('Post', :NEW."postId", v_word, 'Pending', SYSTIMESTAMP);
        END IF;
    END IF;
END;
/

CREATE OR REPLACE TRIGGER "TRG_PostComment_SensitiveStatus"
BEFORE INSERT OR UPDATE OF "content" ON "PostComment"
FOR EACH ROW
DECLARE
    v_word VARCHAR2(200);
BEGIN
    v_word := "fn_find_sensitive_word"(NULL, :NEW."content");
    IF v_word IS NOT NULL THEN
        :NEW."status" := 'PendingReview';
    END IF;
END;
/

CREATE OR REPLACE TRIGGER "TRG_PostComment_SensitiveAudit"
AFTER INSERT OR UPDATE OF "content" ON "PostComment"
FOR EACH ROW
DECLARE
    v_word VARCHAR2(200);
BEGIN
    v_word := "fn_find_sensitive_word"(NULL, :NEW."content");
    IF v_word IS NOT NULL THEN
        INSERT INTO "AuditRecord" ("targetType", "targetId", "triggerWord", "status", "createTime")
        VALUES ('Comment', :NEW."commentId", v_word, 'Pending', SYSTIMESTAMP);
    END IF;
END;
/

-- ============================================================
-- 3. 点赞计数
--    Post.likeCount 是 PostLike 明细的派生汇总，用原子自增维护，
--    取代应用层“读取-加一-回写”的丢更新写法。
-- ============================================================

CREATE OR REPLACE TRIGGER "TRG_PostLike_Count"
AFTER INSERT OR DELETE ON "PostLike"
FOR EACH ROW
BEGIN
    IF INSERTING THEN
        UPDATE "Post"
           SET "likeCount" = NVL("likeCount", 0) + 1
         WHERE "postId" = :NEW."postId";
    ELSE
        UPDATE "Post"
           SET "likeCount" = GREATEST(NVL("likeCount", 0) - 1, 0)
         WHERE "postId" = :OLD."postId";
    END IF;
END;
/

-- ============================================================
-- 4. 用户与钱包的一对一不变式
--    idx_wallet_user 保证唯一，触发器保证存在：新用户注册即有零余额钱包。
-- ============================================================

CREATE OR REPLACE TRIGGER "TRG_User_DefaultWallet"
AFTER INSERT ON "User"
FOR EACH ROW
WHEN (NEW."userId" IS NOT NULL)
BEGIN
    INSERT INTO "Wallet" ("balance", "userId") VALUES (0, :NEW."userId");
END;
/

-- ============================================================
-- 5. 版块必须有版主：创建者自动成为 Moderator
-- ============================================================

CREATE OR REPLACE TRIGGER "TRG_Forum_CreatorManager"
AFTER INSERT ON "Forum"
FOR EACH ROW
WHEN (NEW."creatorId" IS NOT NULL)
BEGIN
    INSERT INTO "ForumManager" ("forumId", "userId", "role")
    VALUES (:NEW."forumId", :NEW."creatorId", 'Moderator');
END;
/

-- ============================================================
-- 6. 通知已读时间：isRead 翻转为已读时自动记录 readTime，回退为未读时清空
-- ============================================================

CREATE OR REPLACE TRIGGER "TRG_Notification_ReadTime"
BEFORE UPDATE OF "isRead" ON "Notification"
FOR EACH ROW
BEGIN
    IF NVL(:NEW."isRead", '0') = '1' AND NVL(:OLD."isRead", '0') <> '1' THEN
        :NEW."readTime" := SYSTIMESTAMP;
    ELSIF NVL(:NEW."isRead", '0') <> '1' AND NVL(:OLD."isRead", '0') = '1' THEN
        :NEW."readTime" := NULL;
    END IF;
END;
/

-- ============================================================
-- 7. 好友关系变更时间：任何状态流转都刷新 updateTime，好友列表按它排序
-- ============================================================

CREATE OR REPLACE TRIGGER "TRG_FriendShip_TouchTime"
BEFORE INSERT OR UPDATE ON "FriendShip"
FOR EACH ROW
BEGIN
    :NEW."updateTime" := SYSTIMESTAMP;
END;
/

-- ============================================================
-- 8. 订单终态归档留言板：订单进入 Completed/Cancelled/Refunded 时，
--    该订单下的留言统一置为已归档，留言板随之只读。
-- ============================================================

CREATE OR REPLACE TRIGGER "TRG_Transaction_ArchiveMsg"
AFTER UPDATE OF "transactionStatus" ON "Transaction"
FOR EACH ROW
BEGIN
    IF :NEW."transactionStatus" IN ('Completed', 'Cancelled', 'Refunded')
       AND NVL(:OLD."transactionStatus", '~') <> :NEW."transactionStatus" THEN
        UPDATE "OrderMessage"
           SET "isArchived" = '1'
         WHERE "transactionId" = :NEW."transactionId"
           AND NVL("isArchived", '0') <> '1';
    END IF;
END;
/

-- ============================================================
-- 9. 库存归零自动锁定商品：下单扣减库存后不再由应用层拼接 CASE 表达式
-- ============================================================

CREATE OR REPLACE TRIGGER "TRG_Product_StockLock"
BEFORE UPDATE OF "stock" ON "Product"
FOR EACH ROW
BEGIN
    IF :NEW."stock" IS NOT NULL AND :NEW."stock" <= 0 AND :NEW."status" = 'Active' THEN
        :NEW."status" := 'Locked';
    END IF;
END;
/

-- ============================================================
-- 校验：本脚本创建的对象必须全部编译通过，否则初始化直接失败
-- ============================================================

WHENEVER SQLERROR EXIT SQL.SQLCODE

DECLARE
    v_invalid NUMBER;
    v_total   NUMBER;
BEGIN
    -- 初始化脚本由 SYS 连接、CURRENT_SCHEMA 指向 APPUSER，
    -- 对象实际属于 APPUSER，因此必须查 all_* 视图并按 owner 过滤，
    -- user_* 视图只能看到当前登录用户（SYS）自己的对象。
    SELECT COUNT(*)
      INTO v_invalid
      FROM all_objects
     WHERE owner = 'APPUSER'
       AND object_type IN ('TRIGGER', 'FUNCTION')
       AND status <> 'VALID';

    IF v_invalid > 0 THEN
        RAISE_APPLICATION_ERROR(-20801, '08_triggers.sql 存在无效对象数量: ' || v_invalid);
    END IF;

    -- 触发器数量兜底：少于 11 个说明有对象未创建成功
    SELECT COUNT(*)
      INTO v_total
      FROM all_triggers
     WHERE owner = 'APPUSER'
       AND trigger_name LIKE 'TRG\_%' ESCAPE '\';

    IF v_total < 11 THEN
        RAISE_APPLICATION_ERROR(-20802, '08_triggers.sql 触发器数量不足: ' || v_total);
    END IF;
END;
/

COMMIT;
