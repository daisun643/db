-- ============================================================
-- 09_procedures.sql
-- Oracle 18c 存储过程与函数：把多表事务、状态机和资金划转下沉到数据库
-- 依赖 08_triggers.sql（留言归档、库存锁定、默认钱包等触发器）
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;
SET DEFINE OFF;
SET SQLBLANKLINES ON;

-- 业务失败统一使用 p_code / p_message 返回，避免异常穿透到应用层后再解析 ORA 文本。
-- p_code = 0 表示成功，其余取值与接口原有的中文提示一一对应。

-- ============================================================
-- 1. 钱包兜底：返回用户钱包 ID，不存在则创建零余额钱包
--    只能在 PL/SQL 中调用（函数内含 DML，不能出现在 SELECT 列表中）。
-- ============================================================

CREATE OR REPLACE FUNCTION "fn_ensure_wallet"(p_user_id IN NUMBER)
RETURN NUMBER
IS
    v_wallet_id NUMBER;
BEGIN
    SELECT "walletId" INTO v_wallet_id FROM "Wallet" WHERE "userId" = p_user_id;
    RETURN v_wallet_id;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        BEGIN
            INSERT INTO "Wallet" ("balance", "userId") VALUES (0, p_user_id)
            RETURNING "walletId" INTO v_wallet_id;
            RETURN v_wallet_id;
        EXCEPTION
            WHEN DUP_VAL_ON_INDEX THEN
                SELECT "walletId" INTO v_wallet_id FROM "Wallet" WHERE "userId" = p_user_id;
                RETURN v_wallet_id;
        END;
END;
/

-- ============================================================
-- 2. 信用分调整：0~1000 夹取、审计流水与用户分值在同一次调用内完成
-- ============================================================

CREATE OR REPLACE PROCEDURE "sp_adjust_credit"(
    p_user_id       IN  NUMBER,
    p_change_points IN  NUMBER,
    p_reason        IN  VARCHAR2,
    p_operator_id   IN  NUMBER DEFAULT NULL,
    p_before_credit OUT NUMBER,
    p_after_credit  OUT NUMBER,
    p_real_change   OUT NUMBER,
    p_adjustment_id OUT NUMBER)
IS
    v_credit NUMBER;
    v_reason VARCHAR2(500);
BEGIN
    p_before_credit := NULL;
    p_after_credit  := NULL;
    p_real_change   := NULL;
    p_adjustment_id := NULL;

    BEGIN
        SELECT NVL("credit", 100) INTO v_credit FROM "User" WHERE "userId" = p_user_id;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RETURN;  -- 用户不存在：由调用方按“调整记录为空”处理
    END;

    v_reason := TRIM(p_reason);
    IF v_reason IS NULL THEN
        v_reason := '信用分调整';
    ELSIF LENGTH(v_reason) > 500 THEN
        v_reason := SUBSTR(v_reason, 1, 500);
    END IF;

    p_before_credit := v_credit;
    p_after_credit  := LEAST(1000, GREATEST(0, v_credit + NVL(p_change_points, 0)));
    p_real_change   := p_after_credit - p_before_credit;

    UPDATE "User" SET "credit" = p_after_credit WHERE "userId" = p_user_id;

    INSERT INTO "CreditAdjustment"
        ("userId", "description", "changePoints", "beforeCredit", "afterCredit", "operatorId", "adjustTime")
    VALUES
        (p_user_id, v_reason, p_real_change, p_before_credit, p_after_credit, p_operator_id, SYSTIMESTAMP)
    RETURNING "creditAdjustmentId" INTO p_adjustment_id;
END;
/

-- ============================================================
-- 3. 钱包充值：余额变更与充值流水原子完成（此前两步写在应用层且未加事务）
-- ============================================================

CREATE OR REPLACE PROCEDURE "sp_wallet_deposit"(
    p_user_id        IN  NUMBER,
    p_amount         IN  NUMBER,
    p_code           OUT NUMBER,
    p_message        OUT VARCHAR2,
    p_balance        OUT NUMBER,
    p_transaction_id OUT NUMBER)
IS
    v_wallet_id NUMBER;
BEGIN
    p_code := 0;
    p_message := NULL;
    p_balance := NULL;
    p_transaction_id := NULL;

    IF p_amount IS NULL OR p_amount <= 0 THEN
        p_code := 1;
        p_message := '充值金额必须大于 0';
        RETURN;
    END IF;

    SAVEPOINT sp_wallet_deposit_start;
    v_wallet_id := "fn_ensure_wallet"(p_user_id);

    UPDATE "Wallet"
       SET "balance" = NVL("balance", 0) + p_amount
     WHERE "walletId" = v_wallet_id
    RETURNING NVL("balance", 0) INTO p_balance;

    -- 充值计入资金流水：与订单流水同构，productId 为空表示非商品交易
    INSERT INTO "Transaction" ("transactionAmount", "transactionStatus", "createTime", "payTime", "userId")
    VALUES (p_amount, 'Completed', SYSTIMESTAMP, SYSTIMESTAMP, p_user_id)
    RETURNING "transactionId" INTO p_transaction_id;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO sp_wallet_deposit_start;
        p_code := 2;
        p_message := '充值失败';
        p_balance := NULL;
        p_transaction_id := NULL;
END;
/

-- ============================================================
-- 4. 浏览量自增：原子累加，取代应用层“读取-加一-回写”的丢更新写法
-- ============================================================

CREATE OR REPLACE PROCEDURE "sp_post_view"(
    p_post_id    IN  NUMBER,
    p_view_count OUT NUMBER)
IS
BEGIN
    p_view_count := NULL;

    UPDATE "Post"
       SET "viewCount" = NVL("viewCount", 0) + 1
     WHERE "postId" = p_post_id
    RETURNING NVL("viewCount", 0) INTO p_view_count;
END;
/

-- ============================================================
-- 5. 下单：商品校验 + 条件扣减库存 + 建单，一步完成防超卖
--    库存归零时由 TRG_Product_StockLock 自动把商品置为 Locked
-- ============================================================

CREATE OR REPLACE PROCEDURE "sp_create_order"(
    p_buyer_id       IN  NUMBER,
    p_product_id     IN  NUMBER,
    p_code           OUT NUMBER,
    p_message        OUT VARCHAR2,
    p_transaction_id OUT NUMBER,
    p_amount         OUT NUMBER,
    p_product_title  OUT VARCHAR2,
    p_seller_id      OUT NUMBER)
IS
    v_status  VARCHAR2(20);
    v_price   NUMBER;
    v_title   VARCHAR2(200);
    v_seller  NUMBER;
BEGIN
    p_code := 1;
    p_message := '商品不可下单';
    p_transaction_id := NULL;
    p_amount := NULL;
    p_product_title := NULL;
    p_seller_id := NULL;

    SAVEPOINT sp_create_order_start;

    BEGIN
        SELECT "status", "price", "title", "userId"
          INTO v_status, v_price, v_title, v_seller
          FROM "Product"
         WHERE "productId" = p_product_id
           FOR UPDATE;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RETURN;
    END;

    IF v_status IS NULL OR v_status <> 'Active' THEN
        RETURN;
    END IF;

    IF v_seller IS NOT NULL AND v_seller = p_buyer_id THEN
        p_code := 2;
        p_message := '不能购买自己发布的商品';
        RETURN;
    END IF;

    UPDATE "Product"
       SET "stock" = NVL("stock", 0) - 1
     WHERE "productId" = p_product_id
       AND NVL("stock", 0) > 0
       AND "status" = 'Active';

    IF SQL%ROWCOUNT <> 1 THEN
        ROLLBACK TO sp_create_order_start;
        p_code := 3;
        p_message := '库存不足或商品已锁定';
        RETURN;
    END IF;

    INSERT INTO "Transaction"
        ("transactionAmount", "transactionStatus", "createTime", "userId", "productId")
    VALUES
        (NVL(v_price, 0), 'Pending', SYSTIMESTAMP, p_buyer_id, p_product_id)
    RETURNING "transactionId" INTO p_transaction_id;

    p_code := 0;
    p_message := NULL;
    p_amount := NVL(v_price, 0);
    p_product_title := v_title;
    p_seller_id := v_seller;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO sp_create_order_start;
        RAISE;
END;
/

-- ============================================================
-- 6. 支付：状态机 Pending → Paid 与买家余额条件扣款原子完成
-- ============================================================

CREATE OR REPLACE PROCEDURE "sp_pay_order"(
    p_transaction_id IN  NUMBER,
    p_buyer_id       IN  NUMBER,
    p_code           OUT NUMBER,
    p_message        OUT VARCHAR2,
    p_balance        OUT NUMBER,
    p_amount         OUT NUMBER,
    p_seller_id      OUT NUMBER)
IS
    v_status  VARCHAR2(20);
    v_amount  NUMBER;
    v_product NUMBER;
    v_seller  NUMBER;
    v_wallet  NUMBER;
BEGIN
    p_code := 1;
    p_message := '当前订单不可支付';
    p_balance := NULL;
    p_amount := NULL;
    p_seller_id := NULL;

    SAVEPOINT sp_pay_order_start;

    BEGIN
        SELECT "transactionStatus", NVL("transactionAmount", 0), "productId"
          INTO v_status, v_amount, v_product
          FROM "Transaction"
         WHERE "transactionId" = p_transaction_id
           FOR UPDATE;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_code := 4;
            p_message := '订单不存在';
            RETURN;
    END;

    IF v_status IS NULL OR v_status <> 'Pending' THEN
        RETURN;
    END IF;

    BEGIN
        SELECT "userId" INTO v_seller FROM "Product" WHERE "productId" = v_product;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            v_seller := NULL;
    END;

    IF v_seller IS NULL THEN
        p_code := 2;
        p_message := '商品卖家不存在';
        RETURN;
    END IF;

    v_wallet := "fn_ensure_wallet"(p_buyer_id);

    -- 余额条件扣款，杜绝透支
    UPDATE "Wallet"
       SET "balance" = NVL("balance", 0) - v_amount
     WHERE "walletId" = v_wallet
       AND NVL("balance", 0) >= v_amount
    RETURNING NVL("balance", 0) INTO p_balance;

    IF SQL%ROWCOUNT <> 1 THEN
        ROLLBACK TO sp_pay_order_start;
        p_code := 3;
        p_message := '钱包余额不足';
        p_balance := NULL;
        RETURN;
    END IF;

    UPDATE "Transaction"
       SET "transactionStatus" = 'Paid',
           "payTime" = SYSTIMESTAMP
     WHERE "transactionId" = p_transaction_id
       AND "transactionStatus" = 'Pending';

    IF SQL%ROWCOUNT <> 1 THEN
        ROLLBACK TO sp_pay_order_start;
        p_code := 1;
        p_message := '当前订单不可支付';
        p_balance := NULL;
        RETURN;
    END IF;

    p_code := 0;
    p_message := NULL;
    p_amount := v_amount;
    p_seller_id := v_seller;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO sp_pay_order_start;
        RAISE;
END;
/

-- ============================================================
-- 7. 确认收货：Paid → Completed，结算卖家钱包，售罄商品转为 Sold
--    留言板归档由 TRG_Transaction_ArchiveMsg 自动完成
-- ============================================================

CREATE OR REPLACE PROCEDURE "sp_confirm_receipt"(
    p_transaction_id IN  NUMBER,
    p_code           OUT NUMBER,
    p_message        OUT VARCHAR2,
    p_amount         OUT NUMBER,
    p_seller_id      OUT NUMBER,
    p_seller_balance OUT NUMBER)
IS
    v_status  VARCHAR2(20);
    v_amount  NUMBER;
    v_product NUMBER;
    v_seller  NUMBER;
    v_stock   NUMBER;
    v_wallet  NUMBER;
BEGIN
    p_code := 1;
    p_message := '当前订单不可确认收货';
    p_amount := NULL;
    p_seller_id := NULL;
    p_seller_balance := NULL;

    SAVEPOINT sp_confirm_receipt_start;

    BEGIN
        SELECT "transactionStatus", NVL("transactionAmount", 0), "productId"
          INTO v_status, v_amount, v_product
          FROM "Transaction"
         WHERE "transactionId" = p_transaction_id
           FOR UPDATE;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_code := 4;
            p_message := '订单不存在';
            RETURN;
    END;

    IF v_status IS NULL OR v_status <> 'Paid' THEN
        RETURN;
    END IF;

    BEGIN
        SELECT "userId", NVL("stock", 0) INTO v_seller, v_stock
          FROM "Product" WHERE "productId" = v_product;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            v_seller := NULL;
            v_stock := 0;
    END;

    IF v_seller IS NULL THEN
        p_code := 2;
        p_message := '商品卖家不存在';
        RETURN;
    END IF;

    UPDATE "Transaction"
       SET "transactionStatus" = 'Completed'
     WHERE "transactionId" = p_transaction_id
       AND "transactionStatus" = 'Paid';

    IF SQL%ROWCOUNT <> 1 THEN
        ROLLBACK TO sp_confirm_receipt_start;
        p_code := 1;
        p_message := '当前订单不可确认收货';
        RETURN;
    END IF;

    v_wallet := "fn_ensure_wallet"(v_seller);

    UPDATE "Wallet"
       SET "balance" = NVL("balance", 0) + v_amount
     WHERE "walletId" = v_wallet
    RETURNING NVL("balance", 0) INTO p_seller_balance;

    IF v_stock <= 0 THEN
        UPDATE "Product" SET "status" = 'Sold' WHERE "productId" = v_product;
    END IF;

    p_code := 0;
    p_message := NULL;
    p_amount := v_amount;
    p_seller_id := v_seller;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO sp_confirm_receipt_start;
        RAISE;
END;
/

-- ============================================================
-- 8. 取消订单：Pending → Cancelled，并回滚库存与商品状态
-- ============================================================

CREATE OR REPLACE PROCEDURE "sp_cancel_order"(
    p_transaction_id IN  NUMBER,
    p_code           OUT NUMBER,
    p_message        OUT VARCHAR2)
IS
    v_status  VARCHAR2(20);
    v_product NUMBER;
BEGIN
    p_code := 1;
    p_message := '只有待支付订单可以取消';

    SAVEPOINT sp_cancel_order_start;

    BEGIN
        SELECT "transactionStatus", "productId"
          INTO v_status, v_product
          FROM "Transaction"
         WHERE "transactionId" = p_transaction_id
           FOR UPDATE;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_code := 4;
            p_message := '订单不存在';
            RETURN;
    END;

    IF v_status IS NULL OR v_status <> 'Pending' THEN
        RETURN;
    END IF;

    UPDATE "Transaction"
       SET "transactionStatus" = 'Cancelled'
     WHERE "transactionId" = p_transaction_id
       AND "transactionStatus" = 'Pending';

    IF SQL%ROWCOUNT <> 1 THEN
        ROLLBACK TO sp_cancel_order_start;
        p_code := 1;
        p_message := '只有待支付订单可以取消';
        RETURN;
    END IF;

    IF v_product IS NOT NULL THEN
        UPDATE "Product"
           SET "stock" = NVL("stock", 0) + 1,
               "status" = 'Active'
         WHERE "productId" = v_product;
    END IF;

    p_code := 0;
    p_message := NULL;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO sp_cancel_order_start;
        RAISE;
END;
/

-- ============================================================
-- 9. 纠纷结案：工单与订单状态流转、买卖双方钱包划转、退款流水与仲裁结果
--    信用分影响仍由应用层按责任方规则调用 sp_adjust_credit
-- ============================================================

CREATE OR REPLACE PROCEDURE "sp_resolve_dispute"(
    p_ticket_id       IN  NUMBER,
    p_refund_amount   IN  NUMBER,
    p_decision        IN  VARCHAR2,
    p_code            OUT NUMBER,
    p_message         OUT VARCHAR2,
    p_transaction_id  OUT NUMBER,
    p_final_status    OUT VARCHAR2,
    p_buyer_id        OUT NUMBER,
    p_seller_id       OUT NUMBER,
    p_order_amount    OUT NUMBER,
    p_seller_amount   OUT NUMBER,
    p_buyer_wallet_id OUT NUMBER)
IS
    v_dispute_status VARCHAR2(20);
    v_order_status   VARCHAR2(20);
    v_amount         NUMBER;
    v_buyer          NUMBER;
    v_product        NUMBER;
    v_seller         NUMBER;
    v_stock          NUMBER;
    v_refund         NUMBER;
    v_seller_amount  NUMBER;
    v_final_status   VARCHAR2(20);
    v_buyer_wallet   NUMBER;
    v_seller_wallet  NUMBER;
BEGIN
    p_code := 1;
    p_message := '该纠纷已处理';
    p_transaction_id := NULL;
    p_final_status := NULL;
    p_buyer_id := NULL;
    p_seller_id := NULL;
    p_order_amount := NULL;
    p_seller_amount := NULL;
    p_buyer_wallet_id := NULL;

    v_refund := NVL(p_refund_amount, 0);

    SAVEPOINT sp_resolve_dispute_start;

    BEGIN
        SELECT "status", "transactionId"
          INTO v_dispute_status, p_transaction_id
          FROM "DisputeTicket"
         WHERE "ticketId" = p_ticket_id
           FOR UPDATE;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_code := 5;
            p_message := '纠纷不存在';
            RETURN;
    END;

    IF v_dispute_status NOT IN ('Open', 'NeedSupplement') THEN
        RETURN;
    END IF;

    BEGIN
        SELECT "transactionStatus", NVL("transactionAmount", 0), "userId", "productId"
          INTO v_order_status, v_amount, v_buyer, v_product
          FROM "Transaction"
         WHERE "transactionId" = p_transaction_id
           FOR UPDATE;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_code := 6;
            p_message := '订单或卖家不存在';
            RETURN;
    END;

    BEGIN
        SELECT "userId", NVL("stock", 0) INTO v_seller, v_stock
          FROM "Product" WHERE "productId" = v_product;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            v_seller := NULL;
            v_stock := 0;
    END;

    IF v_seller IS NULL THEN
        p_code := 6;
        p_message := '订单或卖家不存在';
        RETURN;
    END IF;

    IF v_order_status IS NULL OR v_order_status <> 'Disputed' THEN
        p_code := 2;
        p_message := '订单当前不在纠纷处理中';
        RETURN;
    END IF;

    IF v_refund > v_amount THEN
        p_code := 3;
        p_message := '退款金额不能超过订单金额';
        RETURN;
    END IF;

    v_final_status := CASE WHEN v_refund > 0 THEN 'Refunded' ELSE 'Completed' END;
    v_seller_amount := v_amount - v_refund;

    UPDATE "DisputeTicket"
       SET "status" = 'Resolved',
           "assignTime" = NVL("assignTime", SYSTIMESTAMP)
     WHERE "ticketId" = p_ticket_id
       AND "status" IN ('Open', 'NeedSupplement');

    IF SQL%ROWCOUNT <> 1 THEN
        ROLLBACK TO sp_resolve_dispute_start;
        p_code := 4;
        p_message := '该纠纷或订单状态已变化，不能重复结算';
        RETURN;
    END IF;

    UPDATE "Transaction"
       SET "transactionStatus" = v_final_status
     WHERE "transactionId" = p_transaction_id
       AND "transactionStatus" = 'Disputed';

    IF SQL%ROWCOUNT <> 1 THEN
        ROLLBACK TO sp_resolve_dispute_start;
        p_code := 4;
        p_message := '该纠纷或订单状态已变化，不能重复结算';
        RETURN;
    END IF;

    v_buyer_wallet := "fn_ensure_wallet"(v_buyer);
    v_seller_wallet := "fn_ensure_wallet"(v_seller);

    UPDATE "Wallet" SET "balance" = NVL("balance", 0) + v_refund
     WHERE "walletId" = v_buyer_wallet;
    UPDATE "Wallet" SET "balance" = NVL("balance", 0) + v_seller_amount
     WHERE "walletId" = v_seller_wallet;

    -- 纠纷结案的资金划转计入资金流水：与充值流水同构（无商品关联）。
    -- 退款为 0 时订单转为 Completed，卖家全额收入已由原订单流水体现，不重复记账。
    IF v_final_status = 'Refunded' THEN
        INSERT INTO "Transaction"
            ("transactionAmount", "transactionStatus", "createTime", "payTime", "userId")
        VALUES (v_refund, 'DisputeRefund', SYSTIMESTAMP, SYSTIMESTAMP, v_buyer);

        IF v_seller_amount > 0 THEN
            INSERT INTO "Transaction"
                ("transactionAmount", "transactionStatus", "createTime", "payTime", "userId")
            VALUES (v_seller_amount, 'DisputeSettlement', SYSTIMESTAMP, SYSTIMESTAMP, v_seller);
        END IF;
    END IF;

    IF v_stock <= 0 THEN
        UPDATE "Product" SET "status" = 'Sold' WHERE "productId" = v_product;
    END IF;

    INSERT INTO "ArbitrationResult" ("decision", "refundAmount", "createTime", "disputeTicketId", "walletId")
    VALUES (p_decision, v_refund, SYSTIMESTAMP, p_ticket_id, v_buyer_wallet);

    p_code := 0;
    p_message := NULL;
    p_final_status := v_final_status;
    p_buyer_id := v_buyer;
    p_seller_id := v_seller;
    p_order_amount := v_amount;
    p_seller_amount := v_seller_amount;
    p_buyer_wallet_id := v_buyer_wallet;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO sp_resolve_dispute_start;
        RAISE;
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
    -- 对象实际属于 APPUSER，因此必须查 all_* 视图并按 owner 过滤。
    SELECT COUNT(*)
      INTO v_invalid
      FROM all_objects
     WHERE owner = 'APPUSER'
       AND object_type IN ('PROCEDURE', 'FUNCTION')
       AND status <> 'VALID';

    IF v_invalid > 0 THEN
        RAISE_APPLICATION_ERROR(-20901, '09_procedures.sql 存在无效对象数量: ' || v_invalid);
    END IF;

    SELECT COUNT(*)
      INTO v_total
      FROM all_procedures
     WHERE owner = 'APPUSER'
       AND LOWER(object_name) LIKE 'sp\_%' ESCAPE '\';

    IF v_total < 8 THEN
        RAISE_APPLICATION_ERROR(-20902, '09_procedures.sql 存储过程数量不足: ' || v_total);
    END IF;
END;
/

COMMIT;
