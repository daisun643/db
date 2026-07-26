import pytest


class TestDisputeCreation:

    def test_buyer_can_create_dispute(self, admin_market_client, market_client):
        """买家对已支付订单发起纠纷"""
        product = admin_market_client.create_product("纠纷测试商品", 50.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        disputes_before = market_client.get_disputes().json()
        resp = market_client.create_dispute(order["transactionID"], "商品与描述不符")
        assert resp.status_code == 200
        data = resp.json()
        assert data["reason"] == "商品与描述不符"
        assert data["status"] == "Open"

        disputes_after = market_client.get_disputes().json()
        assert len(disputes_after) >= len(disputes_before) + 1

    def test_seller_can_create_dispute(self, admin_market_client, market_client):
        """卖家也能发起纠纷"""
        product = admin_market_client.create_product("卖家纠纷测试", 30.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        resp = admin_market_client.create_dispute(order["transactionID"], "买家未按约定支付")
        assert resp.status_code == 200

    def test_unrelated_user_cannot_create_dispute(self, admin_market_client, market_client, manager_market_client):
        """非买家非卖家不能创建纠纷"""
        product = admin_market_client.create_product("无关用户纠纷", 20.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        resp = manager_market_client.create_dispute(order["transactionID"], "与我无关")
        assert resp.status_code == 403

    def test_cannot_create_duplicate_dispute(self, admin_market_client, market_client):
        """同一订单不能有多个进行中的纠纷"""
        product = admin_market_client.create_product("重复纠纷测试", 40.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        resp1 = market_client.create_dispute(order["transactionID"], "第一次纠纷")
        assert resp1.status_code == 200

        resp2 = market_client.create_dispute(order["transactionID"], "第二次纠纷")
        assert resp2.status_code == 400

    def test_dispute_updates_order_status(self, admin_market_client, market_client):
        """创建纠纷后订单状态变为 Disputed"""
        product = admin_market_client.create_product("状态测试商品", 60.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        market_client.create_dispute(order["transactionID"], "状态测试")
        my_orders = market_client.get_my_orders().json()
        disputed_order = next((o for o in my_orders if o["transactionID"] == order["transactionID"]), None)
        assert disputed_order is not None
        assert disputed_order["transactionStatus"] == "Disputed"


class TestDisputeResolution:

    def test_arbitrator_can_resolve_dispute(self, admin_market_client, market_client):
        """有权限的仲裁员可以处理纠纷"""
        product = admin_market_client.create_product("仲裁测试商品", 100.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "请求仲裁").json()
        dispute_id = dispute["ticketID"]

        resolve_resp = admin_market_client.resolve_dispute(dispute_id, "卖家责任，全额退款", refund_amount=100.0)
        assert resolve_resp.status_code == 200

    def test_non_arbitrator_cannot_resolve(self, market_client, admin_market_client):
        """普通用户不能处理纠纷"""
        product = admin_market_client.create_product("权限测试商品", 80.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "权限测试").json()
        resp = market_client.resolve_dispute(dispute["ticketID"], "自行处理", refund_amount=0)
        assert resp.status_code == 403

    def test_partial_refund_correct_amount(self, admin_market_client, market_client):
        """部分退款金额计算正确"""
        product = admin_market_client.create_product("部分退款测试", 200.0, stock=5).json()
        market_client.deposit(300)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "部分退款测试").json()

        before_wallet = market_client.get_wallet().json()
        before_balance = before_wallet["balance"]

        refund = 80.0
        admin_market_client.resolve_dispute(dispute["ticketID"], "部分退款", refund_amount=refund)

        after_wallet = market_client.get_wallet().json()
        assert after_wallet["balance"] >= before_balance + refund

    def test_resolved_dispute_status(self, admin_market_client, market_client):
        """纠纷处理后状态变为 Resolved"""
        product = admin_market_client.create_product("解决状态测试", 30.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "解决状态").json()
        admin_market_client.resolve_dispute(dispute["ticketID"], "已解决", refund_amount=30.0)

        disputes = admin_market_client.get_disputes().json()
        resolved = next((d for d in disputes if d["ticketID"] == dispute["ticketID"]), None)
        assert resolved is not None
        assert resolved["status"] == "Resolved"

    def test_dispute_shows_after_order_completed(self, admin_market_client, market_client):
        """纠纷处理完成后订单状态变更"""
        product = admin_market_client.create_product("完成态测试", 50.0, stock=5).json()
        market_client.deposit(200)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "完成态测试").json()
        admin_market_client.resolve_dispute(dispute["ticketID"], "买家胜诉", refund_amount=50.0)

        my_orders = market_client.get_my_orders().json()
        completed = next((o for o in my_orders if o["transactionID"] == order["transactionID"]), None)
        assert completed is not None
        assert completed["transactionStatus"] in ("Refunded", "Completed")
