import pytest


class TestWallet:

    def test_get_wallet(self, market_client):
        resp = market_client.get_wallet()
        assert resp.status_code == 200
        data = resp.json()
        assert "walletID" in data
        assert "balance" in data
        assert "userID" in data

    def test_deposit(self, market_client):
        before = market_client.get_wallet().json()["balance"]
        resp = market_client.deposit(100)
        assert resp.status_code == 200
        after = resp.json()["balance"]
        assert after >= before + 100

    def test_deposit_negative_amount(self, market_client):
        resp = market_client.deposit(-50)
        assert resp.status_code in (200, 400)

    def test_wallet_unauthenticated(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.get_wallet()
        assert resp.status_code == 401


class TestProductList:

    def test_get_products_returns_list(self, market_client):
        resp = market_client.get_products()
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)

    def test_product_has_expected_fields(self, market_client):
        resp = market_client.get_products()
        products = resp.json()
        if len(products) > 0:
            p = products[0]
            assert "productID" in p
            assert "title" in p
            assert "description" in p
            assert "price" in p
            assert "stock" in p
            assert "category" in p
            assert "condition" in p
            assert "status" in p
            assert "publishTime" in p
            assert "userID" in p
            assert "sellerName" in p
            assert "imageUrls" in p

    def test_get_products_filter_status(self, market_client):
        resp = market_client.get_products(status="Active")
        assert resp.status_code == 200
        for p in resp.json():
            assert p["status"] == "Active"

    def test_get_product_by_id(self, market_client):
        products = market_client.get_products().json()
        if len(products) > 0:
            pid = products[0]["productID"]
            resp = market_client.get_product(pid)
            assert resp.status_code == 200
            assert resp.json()["productID"] == pid

    def test_get_nonexistent_product(self, market_client):
        resp = market_client.get_product(99999)
        assert resp.status_code == 404

    def test_unauthenticated_can_list_products(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.get_products()
        assert resp.status_code == 200

    def test_get_products_filter_category(self, market_client):
        resp = market_client.get_products(category="数码设备")
        assert resp.status_code == 200
        assert isinstance(resp.json(), list)

    def test_get_products_filter_condition(self, market_client):
        resp = market_client.get_products(condition="良好")
        assert resp.status_code == 200
        assert isinstance(resp.json(), list)

    def test_get_products_filter_stock_range(self, market_client):
        resp = market_client.get_products(min_stock=1, max_stock=20)
        assert resp.status_code == 200
        assert isinstance(resp.json(), list)

    def test_get_products_filter_price_range(self, market_client):
        resp = market_client.get_products(min_price=5, max_price=20)
        assert resp.status_code == 200
        assert isinstance(resp.json(), list)

    def test_get_products_sort_price_desc(self, market_client):
        resp = market_client.get_products(sort="price-desc", page_size=20)
        assert resp.status_code == 200
        products = resp.json()
        if len(products) > 1:
            prices = [item["price"] for item in products]
            assert prices == sorted(prices, reverse=True)

    def test_get_products_returns_total_count(self, market_client):
        resp = market_client.get_products(page=1, page_size=1)
        assert resp.status_code == 200
        assert "x-total-count" in {k.lower() for k in resp.headers.keys()}

    def test_get_products_invalid_sort_rejected(self, market_client):
        resp = market_client.get_products(sort="invalid")
        assert resp.status_code == 400


class TestProductCRUD:

    def test_admin_can_create_product(self, admin_market_client):
        resp = admin_market_client.create_product("测试商品", 9.99, stock=5, description="测试描述")
        assert resp.status_code in (200, 201)
        data = resp.json()
        assert data["title"] == "测试商品"
        assert data["price"] == 9.99
        assert data["stock"] == 5
        assert data["category"] == "其他"
        assert data["condition"] == "良好"
        assert data["status"] == "Active"

    def test_create_product_with_category_and_condition(self, admin_market_client):
        resp = admin_market_client.create_product(
            "分类成色商品",
            19.9,
            stock=2,
            category="数码设备",
            condition="几乎全新",
        )
        assert resp.status_code == 201
        data = resp.json()
        assert data["category"] == "数码设备"
        assert data["condition"] == "几乎全新"

    def test_user_can_create_product(self, market_client):
        resp = market_client.create_product("合法商品", 1.0, stock=1)
        assert resp.status_code == 201

    def test_unauthenticated_create_product_fails(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.create_product("未登录发布商品", 1.0, stock=1)
        assert resp.status_code == 401

    def test_low_credit_user_cannot_create_product(self, admin_market_client, market_client):
        profile = market_client.get("/api/user/profile").json()
        user_id = profile["userId"]
        original_credit = profile.get("credit") or 0

        try:
            admin_market_client.post("/api/user/credit/add", json={
                "userId": user_id,
                "credit": -1000,
                "reason": "商品发布低信用限制测试",
            })
            resp = market_client.create_product("低信用发布商品", 1.0, stock=1)
            assert resp.status_code == 400
        finally:
            current = market_client.get("/api/user/profile").json().get("credit") or 0
            restore = original_credit - current
            if restore:
                admin_market_client.post("/api/user/credit/add", json={
                    "userId": user_id,
                    "credit": restore,
                    "reason": "恢复商品发布低信用限制测试信用分",
                })

    def test_admin_can_update_product(self, admin_market_client):
        create_resp = admin_market_client.create_product("待修改商品", 5.0, stock=3)
        pid = create_resp.json()["productID"]
        resp = admin_market_client.update_product(pid, "修改后商品", 8.0, stock=6, description="新描述")
        assert resp.status_code == 200
        assert resp.json()["title"] == "修改后商品"
        assert resp.json()["price"] == 8.0
        assert resp.json()["stock"] == 6

    def test_seller_can_update_own_active_product(self, market_client):
        create_resp = market_client.create_product("自己的可编辑商品", 5.0, stock=3)
        pid = create_resp.json()["productID"]
        resp = market_client.update_product(
            pid,
            "自己修改后的商品",
            6.0,
            stock=4,
            category="教材资料",
            condition="有使用痕迹",
        )
        assert resp.status_code == 200
        data = resp.json()
        assert data["title"] == "自己修改后的商品"
        assert data["category"] == "教材资料"
        assert data["condition"] == "有使用痕迹"

    def test_user_cannot_update_others_product(self, admin_market_client, market_client):
        create_resp = admin_market_client.create_product("受保护商品", 10.0, stock=2)
        pid = create_resp.json()["productID"]
        resp = market_client.update_product(pid, "恶意修改", 1.0)
        assert resp.status_code == 403

    def test_admin_can_delete_product(self, admin_market_client):
        create_resp = admin_market_client.create_product("待下架商品", 3.0, stock=1)
        pid = create_resp.json()["productID"]
        resp = admin_market_client.delete_product(pid)
        assert resp.status_code == 200

    def test_user_cannot_delete_others_product(self, admin_market_client, market_client):
        create_resp = admin_market_client.create_product("不可删商品", 7.0, stock=2)
        pid = create_resp.json()["productID"]
        resp = market_client.delete_product(pid)
        assert resp.status_code == 403

    def test_update_nonexistent_product(self, admin_market_client):
        resp = admin_market_client.update_product(99999, "不存在", 1.0)
        assert resp.status_code == 404

    def test_delete_nonexistent_product(self, admin_market_client):
        resp = admin_market_client.delete_product(99999)
        assert resp.status_code == 404

    def test_get_my_products(self, admin_market_client):
        admin_market_client.create_product("我的商品A", 1.0, stock=1)
        resp = admin_market_client.get_my_products()
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)
        assert len(data) >= 1

    def test_create_product_missing_fields(self, admin_market_client):
        resp = admin_market_client.post("/api/products", json={})
        assert resp.status_code in (400, 403)


class TestProductStatus:

    def test_change_status_lock(self, admin_market_client):
        create_resp = admin_market_client.create_product("待锁定商品", 5.0, stock=3)
        pid = create_resp.json()["productID"]
        resp = admin_market_client.change_product_status(pid, "lock")
        assert resp.status_code == 200
        assert resp.json()["status"] == "Locked"

    def test_change_status_inactive(self, admin_market_client):
        create_resp = admin_market_client.create_product("待下架商品2", 5.0, stock=3)
        pid = create_resp.json()["productID"]
        resp = admin_market_client.change_product_status(pid, "off-shelf")
        assert resp.status_code == 200
        assert resp.json()["status"] == "Inactive"

    def test_change_status_invalid_action(self, admin_market_client):
        create_resp = admin_market_client.create_product("测试无效动作", 5.0, stock=3)
        pid = create_resp.json()["productID"]
        resp = admin_market_client.change_product_status(pid, "invalid_action")
        assert resp.status_code == 400

    def test_change_status_nonexistent_product(self, admin_market_client):
        resp = admin_market_client.change_product_status(99999, "lock")
        assert resp.status_code == 404

    def test_sold_product_cannot_be_edited(self, admin_market_client):
        create_resp = admin_market_client.create_product("已售出商品", 5.0, stock=1)
        pid = create_resp.json()["productID"]
        admin_market_client.change_product_status(pid, "sold")
        resp = admin_market_client.update_product(pid, "修改已售商品", 10.0)
        assert resp.status_code == 400

    def test_inactive_product_cannot_be_edited(self, admin_market_client):
        create_resp = admin_market_client.create_product("已下架不可编辑商品", 5.0, stock=1)
        pid = create_resp.json()["productID"]
        admin_market_client.change_product_status(pid, "off-shelf")
        resp = admin_market_client.update_product(pid, "修改已下架商品", 10.0)
        assert resp.status_code == 400

    def test_sold_product_cannot_be_restored(self, admin_market_client):
        create_resp = admin_market_client.create_product("售出不可上架商品", 5.0, stock=1)
        pid = create_resp.json()["productID"]
        admin_market_client.change_product_status(pid, "sold")
        resp = admin_market_client.change_product_status(pid, "restore")
        assert resp.status_code == 400

    def test_user_cannot_change_others_product_status(self, admin_market_client, market_client):
        create_resp = admin_market_client.create_product("别人的商品", 5.0, stock=3)
        pid = create_resp.json()["productID"]
        resp = market_client.change_product_status(pid, "lock")
        assert resp.status_code == 403


class TestOrderCreate:

    def test_create_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("可购买商品", 10.0, stock=5).json()
        resp = market_client.create_order(product["productID"])
        assert resp.status_code == 200
        data = resp.json()
        assert data["transactionAmount"] == 10.0
        assert data["transactionStatus"] == "Pending"
        assert data["productID"] == product["productID"]

    def test_cannot_buy_own_product(self, admin_market_client):
        product = admin_market_client.create_product("自己的商品", 10.0, stock=5).json()
        resp = admin_market_client.create_order(product["productID"])
        assert resp.status_code == 400

    def test_create_order_nonexistent_product(self, market_client):
        resp = market_client.create_order(99999)
        assert resp.status_code in (400, 404)

    def test_create_order_inactive_product(self, admin_market_client, market_client):
        product = admin_market_client.create_product("已下架商品", 5.0, stock=3).json()
        admin_market_client.change_product_status(product["productID"], "inactive")
        resp = market_client.create_order(product["productID"])
        assert resp.status_code == 400

    def test_single_stock_product_is_locked_after_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("单库存锁定商品", 10.0, stock=1).json()
        order_resp = market_client.create_order(product["productID"])
        assert order_resp.status_code == 200
        product_resp = admin_market_client.get_product(product["productID"])
        assert product_resp.status_code == 200
        data = product_resp.json()
        assert data["stock"] == 0
        assert data["status"] == "Locked"

    def test_repeat_order_does_not_oversell(self, admin_market_client, market_client, manager_market_client):
        product = admin_market_client.create_product("防超卖商品", 10.0, stock=1).json()
        first_resp = market_client.create_order(product["productID"])
        second_resp = manager_market_client.create_order(product["productID"])

        assert first_resp.status_code == 200
        assert second_resp.status_code == 400

        product_resp = admin_market_client.get_product(product["productID"])
        data = product_resp.json()
        assert data["stock"] == 0
        assert data["status"] == "Locked"

    def test_order_unauthenticated(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.create_order(1)
        assert resp.status_code == 401


class TestOrderPay:

    def test_pay_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("支付测试商品", 20.0, stock=5).json()
        market_client.deposit(100)
        order = market_client.create_order(product["productID"]).json()
        resp = market_client.pay_order(order["transactionID"])
        assert resp.status_code == 200
        data = resp.json()
        assert data["status"] == "Paid"

    def test_pay_insufficient_balance(self, admin_market_client, market_client):
        product = admin_market_client.create_product("高价商品", 99999.0, stock=2).json()
        order = market_client.create_order(product["productID"]).json()
        resp = market_client.pay_order(order["transactionID"])
        assert resp.status_code == 400

    def test_pay_nonexistent_order(self, market_client):
        resp = market_client.pay_order(99999)
        assert resp.status_code == 404

    def test_cannot_pay_others_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("他人订单测试", 5.0, stock=3).json()
        market_client.deposit(50)
        order = market_client.create_order(product["productID"]).json()
        resp = admin_market_client.pay_order(order["transactionID"])
        assert resp.status_code == 404


class TestOrderConfirmReceipt:

    def test_confirm_receipt(self, admin_market_client, market_client):
        product = admin_market_client.create_product("确认收货商品", 15.0, stock=5).json()
        market_client.deposit(100)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])
        resp = market_client.confirm_receipt(order["transactionID"])
        assert resp.status_code == 200
        assert resp.json()["status"] == "Completed"

    def test_confirm_receipt_before_pay(self, admin_market_client, market_client):
        product = admin_market_client.create_product("未支付商品", 10.0, stock=5).json()
        order = market_client.create_order(product["productID"]).json()
        resp = market_client.confirm_receipt(order["transactionID"])
        assert resp.status_code == 400

    def test_confirm_receipt_nonexistent(self, market_client):
        resp = market_client.confirm_receipt(99999)
        assert resp.status_code == 404


class TestOrderCancel:

    def test_cancel_pending_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("可取消商品", 8.0, stock=5).json()
        order = market_client.create_order(product["productID"]).json()
        resp = market_client.cancel_order(order["transactionID"])
        assert resp.status_code == 200
        assert resp.json()["status"] == "Cancelled"

    def test_cannot_cancel_paid_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("已支付取消测试", 10.0, stock=5).json()
        market_client.deposit(100)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])
        resp = market_client.cancel_order(order["transactionID"])
        assert resp.status_code == 400

    def test_cancel_nonexistent_order(self, market_client):
        resp = market_client.cancel_order(99999)
        assert resp.status_code == 404


class TestOrderList:

    def test_get_my_orders(self, admin_market_client, market_client):
        product = admin_market_client.create_product("订单列表商品", 5.0, stock=5).json()
        market_client.create_order(product["productID"])
        resp = market_client.get_my_orders()
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)
        assert len(data) >= 1

    def test_order_has_expected_fields(self, admin_market_client, market_client):
        product = admin_market_client.create_product("字段检查商品", 3.0, stock=5).json()
        market_client.create_order(product["productID"])
        orders = market_client.get_my_orders().json()
        if len(orders) > 0:
            o = orders[0]
            assert "transactionID" in o
            assert "transactionAmount" in o
            assert "transactionStatus" in o
            assert "createTime" in o
            assert "userID" in o
            assert "productID" in o
            assert "productTitle" in o

    def test_get_my_sales(self, admin_market_client, market_client):
        product = admin_market_client.create_product("销售列表商品", 6.0, stock=5).json()
        market_client.create_order(product["productID"])
        resp = admin_market_client.get_my_sales()
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)
        assert len(data) >= 1

    def test_get_my_orders_with_status_filter(self, admin_market_client, market_client):
        product = admin_market_client.create_product("分页订单商品", 12.0, stock=5).json()
        market_client.create_order(product["productID"])
        market_client.create_order(product["productID"])
        pending = market_client.get_my_orders(status="Pending")
        assert pending.status_code == 200
        if len(pending.json()) > 0:
            assert pending.json()[0]["transactionStatus"] == "Pending"

    def test_get_my_orders_returns_total_count(self, admin_market_client, market_client):
        product = admin_market_client.create_product("分页订单商品2", 12.0, stock=5).json()
        market_client.create_order(product["productID"])
        market_client.create_order(admin_market_client.create_product("分页订单商品3", 13.0, stock=5).json()["productID"])
        resp = market_client.get_my_orders(page=1, page_size=1)
        assert resp.status_code == 200
        assert "x-total-count" in {k.lower() for k in resp.headers.keys()}

    def test_get_my_orders_invalid_status_rejected(self, market_client):
        resp = market_client.get_my_orders(status="BAD")
        assert resp.status_code == 400

    def test_get_my_sales_with_status_filter(self, admin_market_client, market_client):
        product = admin_market_client.create_product("销售订单商品", 18.0, stock=5).json()
        market_client.create_order(product["productID"])
        pending = admin_market_client.get_my_sales(status="Pending")
        assert pending.status_code == 200
        if len(pending.json()) > 0:
            assert pending.json()[0]["transactionStatus"] == "Pending"

    def test_get_my_sales_returns_total_count(self, admin_market_client, market_client):
        product = admin_market_client.create_product("销售订单计数", 20.0, stock=5).json()
        market_client.create_order(product["productID"])
        market_client.create_order(admin_market_client.create_product("销售订单计数2", 21.0, stock=5).json()["productID"])
        resp = admin_market_client.get_my_sales(page=1, page_size=1)
        assert resp.status_code == 200
        assert "x-total-count" in {k.lower() for k in resp.headers.keys()}

    def test_get_my_sales_invalid_status_rejected(self, admin_market_client):
        resp = admin_market_client.get_my_sales(status="BAD")
        assert resp.status_code == 400

    def test_orders_unauthenticated(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.get_my_orders()
        assert resp.status_code == 401

    def test_sales_unauthenticated(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.get_my_sales()
        assert resp.status_code == 401


class TestOrderMessages:

    def test_send_and_get_order_message(self, admin_market_client, market_client):
        product = admin_market_client.create_product("留言测试商品", 5.0, stock=5).json()
        market_client.deposit(50)
        order = market_client.create_order(product["productID"]).json()
        oid = order["transactionID"]

        send_resp = market_client.send_order_message(oid, "请问什么时候发货？")
        assert send_resp.status_code == 200
        msg = send_resp.json()
        assert msg["content"] == "请问什么时候发货？"
        assert msg["senderID"] == order["userID"]

        messages = market_client.get_order_messages(oid).json()
        assert isinstance(messages, list)
        assert len(messages) >= 1
        assert messages[0]["content"] == "请问什么时候发货？"

    def test_seller_can_send_order_message(self, admin_market_client, market_client):
        product = admin_market_client.create_product("卖家留言商品", 5.0, stock=5).json()
        market_client.deposit(50)
        order = market_client.create_order(product["productID"]).json()
        oid = order["transactionID"]

        resp = admin_market_client.send_order_message(oid, "明天发货")
        assert resp.status_code == 200
        assert resp.json()["content"] == "明天发货"

    def test_message_on_completed_order_readonly(self, admin_market_client, market_client):
        product = admin_market_client.create_product("已完成留言商品", 5.0, stock=5).json()
        market_client.deposit(50)
        order = market_client.create_order(product["productID"]).json()
        oid = order["transactionID"]
        market_client.pay_order(oid)
        market_client.confirm_receipt(oid)

        resp = market_client.send_order_message(oid, "已完成订单留言")
        assert resp.status_code == 400

    def test_get_messages_nonexistent_order(self, market_client):
        resp = market_client.get_order_messages(99999)
        assert resp.status_code == 404

    def test_send_message_nonexistent_order(self, market_client):
        resp = market_client.send_order_message(99999, "不存在")
        assert resp.status_code == 404

    def test_non_participant_cannot_view_messages(self, admin_market_client, market_client, manager_market_client):
        product = admin_market_client.create_product("非参与方商品", 5.0, stock=5).json()
        market_client.deposit(50)
        order = market_client.create_order(product["productID"]).json()
        oid = order["transactionID"]

        resp = manager_market_client.get_order_messages(oid)
        assert resp.status_code == 404


class TestDisputes:

    def test_get_disputes_empty_or_list(self, market_client):
        resp = market_client.get_disputes()
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)

    def test_admin_can_view_all_disputes(self, admin_market_client):
        resp = admin_market_client.get_disputes()
        assert resp.status_code == 200
        assert isinstance(resp.json(), list)

    def test_create_dispute_on_paid_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("纠纷测试商品", 25.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        resp = market_client.create_dispute(order["transactionID"], "商品质量有问题")
        assert resp.status_code == 200
        data = resp.json()
        assert data["reason"] == "商品质量有问题"
        assert data["status"] == "Open"
        assert data["transactionID"] == order["transactionID"]

    def test_cannot_create_dispute_on_pending_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("未支付纠纷商品", 10.0, stock=5).json()
        order = market_client.create_order(product["productID"]).json()

        resp = market_client.create_dispute(order["transactionID"], "想退款")
        assert resp.status_code == 400

    def test_cannot_create_dispute_on_nonexistent_order(self, market_client):
        resp = market_client.create_dispute(99999, "不存在")
        assert resp.status_code == 404

    def test_dispute_has_expected_fields(self, admin_market_client, market_client):
        product = admin_market_client.create_product("纠纷字段商品", 10.0, stock=5).json()
        market_client.deposit(100)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        resp = market_client.create_dispute(order["transactionID"], "检查字段")
        data = resp.json()
        assert "ticketID" in data
        assert "reason" in data
        assert "status" in data
        assert "createTime" in data
        assert "transactionID" in data
        assert "transactionAmount" in data
        assert "userID" in data

    def test_resolve_dispute_full_refund(self, admin_market_client, market_client):
        product = admin_market_client.create_product("全额退款商品", 30.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "要求全额退款").json()
        resp = admin_market_client.resolve_dispute(dispute["ticketID"], "卖家违约，全额退款", refund_amount=30.0)
        assert resp.status_code == 200
        assert resp.json()["status"] == "Resolved"

    def test_resolve_dispute_partial_refund(self, admin_market_client, market_client):
        product = admin_market_client.create_product("部分退款商品", 40.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "部分质量问题").json()
        resp = admin_market_client.resolve_dispute(dispute["ticketID"], "部分退款", refund_amount=20.0)
        assert resp.status_code == 200
        assert resp.json()["status"] == "Resolved"

    def test_resolve_dispute_no_refund(self, admin_market_client, market_client):
        product = admin_market_client.create_product("不退款商品", 20.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "恶意退款").json()
        resp = admin_market_client.resolve_dispute(dispute["ticketID"], "买家无理，不退款", refund_amount=0)
        assert resp.status_code == 200
        assert resp.json()["status"] == "Resolved"

    def test_resolve_dispute_exceeds_amount(self, admin_market_client, market_client):
        product = admin_market_client.create_product("超额退款商品", 15.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "要求超额退款").json()
        resp = admin_market_client.resolve_dispute(dispute["ticketID"], "超额退款", refund_amount=999.0)
        assert resp.status_code == 400

    def test_cannot_resolve_already_resolved_dispute(self, admin_market_client, market_client):
        product = admin_market_client.create_product("重复结算商品", 10.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "测试重复结算").json()
        admin_market_client.resolve_dispute(dispute["ticketID"], "已处理", refund_amount=0)
        resp = admin_market_client.resolve_dispute(dispute["ticketID"], "再处理", refund_amount=0)
        assert resp.status_code == 400

    def test_resolve_nonexistent_dispute(self, admin_market_client):
        resp = admin_market_client.resolve_dispute(99999, "不存在", refund_amount=0)
        assert resp.status_code == 404

    def test_user_cannot_resolve_dispute(self, admin_market_client, market_client):
        product = admin_market_client.create_product("用户仲裁商品", 10.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "测试用户仲裁").json()
        resp = market_client.resolve_dispute(dispute["ticketID"], "自行裁决", refund_amount=5.0)
        assert resp.status_code == 403

    def test_disputes_unauthenticated(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.get_disputes()
        assert resp.status_code == 401


class TestFullTransactionFlow:

    def test_full_buy_flow(self, admin_market_client, market_client):
        product = admin_market_client.create_product("完整流程商品", 50.0, stock=10).json()

        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        assert order["transactionStatus"] == "Pending"

        pay_resp = market_client.pay_order(order["transactionID"])
        assert pay_resp.status_code == 200
        assert pay_resp.json()["status"] == "Paid"

        confirm_resp = market_client.confirm_receipt(order["transactionID"])
        assert confirm_resp.status_code == 200
        assert confirm_resp.json()["status"] == "Completed"

    def test_cancel_then_reorder(self, admin_market_client, market_client):
        product = admin_market_client.create_product("取消再购商品", 12.0, stock=10).json()

        order = market_client.create_order(product["productID"]).json()
        cancel_resp = market_client.cancel_order(order["transactionID"])
        assert cancel_resp.status_code == 200

        market_client.deposit(100)
        order2 = market_client.create_order(product["productID"])
        assert order2.status_code == 200

    def test_dispute_flow(self, admin_market_client, market_client):
        product = admin_market_client.create_product("纠纷流程商品", 35.0, stock=10).json()

        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "纠纷流程测试").json()
        assert dispute["status"] == "Open"

        resolve_resp = admin_market_client.resolve_dispute(dispute["ticketID"], "部分退款", refund_amount=15.0)
        assert resolve_resp.status_code == 200
        assert resolve_resp.json()["status"] == "Resolved"
