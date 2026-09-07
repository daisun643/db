-- ============================================================
-- 10_views.sql
-- Oracle 18c 视图：把资金流水的口径固化在数据库侧
-- 依赖 09_procedures.sql（流水行由 sp_wallet_deposit / sp_resolve_dispute 等产生）
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;
SET DEFINE OFF;

-- ============================================================
-- 资金流水视图：一行 = 某个用户的一笔收入或支出
--   支出：本人作为买家的商品订单（Paid / Completed / Disputed / Refunded）
--   收入：本人作为卖家的已完成订单、钱包充值、纠纷退款与纠纷结算
-- 应用层只需按 "userId" 过滤并汇总，口径变更只改这一处。
-- ============================================================

CREATE OR REPLACE VIEW "V_FinanceFlow" AS
SELECT t."transactionId"                AS "transactionId",
       t."userId"                       AS "userId",
       '支出'                           AS "flowType",
       NVL(t."transactionAmount", 0)    AS "amount",
       t."transactionStatus"            AS "status",
       NVL(t."payTime", t."createTime") AS "flowTime",
       CASE
           WHEN p."productId" IS NULL THEN '订单消费（商品已下架）'
           ELSE '购买商品：' || p."title"
       END                              AS "description"
  FROM "Transaction" t
  LEFT JOIN "Product" p ON p."productId" = t."productId"
 WHERE t."productId" IS NOT NULL
   AND t."transactionStatus" IN ('Paid', 'Completed', 'Disputed', 'Refunded')
UNION ALL
SELECT t."transactionId",
       p."userId",
       '收入',
       NVL(t."transactionAmount", 0),
       t."transactionStatus",
       NVL(t."payTime", t."createTime"),
       '出售商品：' || p."title"
  FROM "Transaction" t
  JOIN "Product" p ON p."productId" = t."productId"
 WHERE t."transactionStatus" = 'Completed'
UNION ALL
SELECT t."transactionId",
       t."userId",
       '收入',
       NVL(t."transactionAmount", 0),
       t."transactionStatus",
       NVL(t."payTime", t."createTime"),
       '钱包充值'
  FROM "Transaction" t
 WHERE t."productId" IS NULL
   AND t."userId" IS NOT NULL
   AND t."transactionStatus" = 'Completed'
UNION ALL
SELECT t."transactionId",
       t."userId",
       '收入',
       NVL(t."transactionAmount", 0),
       t."transactionStatus",
       NVL(t."payTime", t."createTime"),
       CASE t."transactionStatus"
           WHEN 'DisputeRefund' THEN '纠纷退款'
           ELSE '纠纷结算'
       END
  FROM "Transaction" t
 WHERE t."userId" IS NOT NULL
   AND t."transactionStatus" IN ('DisputeRefund', 'DisputeSettlement');

-- ============================================================
-- 校验：视图必须编译通过，否则初始化直接失败
-- ============================================================

WHENEVER SQLERROR EXIT SQL.SQLCODE

DECLARE
    v_valid NUMBER;
BEGIN
    -- 初始化脚本由 SYS 连接、CURRENT_SCHEMA 指向 APPUSER，
    -- 视图实际属于 APPUSER，因此必须查 all_objects 并按 owner 过滤。
    SELECT COUNT(*)
      INTO v_valid
      FROM all_objects
     WHERE owner = 'APPUSER'
       AND object_type = 'VIEW'
       AND object_name = 'V_FinanceFlow'
       AND status = 'VALID';

    IF v_valid <> 1 THEN
        RAISE_APPLICATION_ERROR(-21001, '10_views.sql 视图 V_FinanceFlow 缺失或无效');
    END IF;
END;
/

COMMIT;
