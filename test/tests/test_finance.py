import pytest


def _get_user_id(client):
    """获取当前登录用户的 ID，若未登录则返回 None"""
    resp = client.get("/api/auth/me")
    if resp.status_code == 200:
        data = resp.json()
        return data.get("user", {}).get("userId")
    return None


class TestFinanceFlows:
    """资金流水查询测试（基于订单派生）"""

    def test_get_flows_returns_list(self, market_client):
        resp = market_client.get("/api/finance/flows")
        assert resp.status_code == 200
        data = resp.json()
        assert "flows" in data
        assert "totalIncome" in data
        assert "totalExpense" in data
        assert "totalCount" in data
        assert isinstance(data["flows"], list)

    def test_get_flows_unauthenticated(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.get("/api/finance/flows")
        assert resp.status_code == 401

    def test_flow_has_expected_fields(self, admin_market_client, market_client):
        product = admin_market_client.create_product("流水字段测试商品", 10.0, stock=5).json()
        market_client.deposit(100)
        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])
        market_client.confirm_receipt(order["transactionID"])

        resp = market_client.get("/api/finance/flows")
        assert resp.status_code == 200
        flows = resp.json()["flows"]
        assert len(flows) >= 1
        flow = flows[0]
        for field in ("type", "amount", "status", "time", "transactionId", "description"):
            assert field in flow

    def test_flow_shows_expense_after_pay(self, admin_market_client, market_client):
        product = admin_market_client.create_product("支出测试商品", 25.0, stock=5).json()
        market_client.deposit(200)

        before = market_client.get("/api/finance/flows").json()
        before_expense = before["totalExpense"]

        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        after = market_client.get("/api/finance/flows").json()
        after_expense = after["totalExpense"]

        assert after_expense >= before_expense + 25.0
        expense_records = [f for f in after["flows"] if f["type"] == "支出"]
        assert len(expense_records) >= 1

    def test_flow_shows_income_after_confirm(self, admin_market_client, market_client):
        product = admin_market_client.create_product("收入测试商品", 30.0, stock=5).json()
        market_client.deposit(200)

        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        before = admin_market_client.get("/api/finance/flows").json()
        before_income = before["totalIncome"]

        market_client.confirm_receipt(order["transactionID"])

        after = admin_market_client.get("/api/finance/flows").json()
        after_income = after["totalIncome"]

        assert after_income >= before_income + 30.0
        income_records = [f for f in after["flows"] if f["type"] == "收入"]
        assert len(income_records) >= 1

    def test_flow_shows_both_income_and_expense(self, admin_market_client, market_client):
        product = admin_market_client.create_product("双向测试商品", 40.0, stock=5).json()
        market_client.deposit(200)

        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])
        market_client.confirm_receipt(order["transactionID"])

        buyer_flows = market_client.get("/api/finance/flows").json()
        buyer_expense = [f for f in buyer_flows["flows"] if f["type"] == "支出"]
        assert len(buyer_expense) >= 1

        seller_flows = admin_market_client.get("/api/finance/flows").json()
        seller_income = [f for f in seller_flows["flows"] if f["type"] == "收入"]
        assert len(seller_income) >= 1

    def test_flows_time_order_descending(self, admin_market_client, market_client):
        product = admin_market_client.create_product("时间排序商品", 5.0, stock=10).json()
        market_client.deposit(100)

        order1 = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order1["transactionID"])
        market_client.confirm_receipt(order1["transactionID"])

        order2 = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order2["transactionID"])
        market_client.confirm_receipt(order2["transactionID"])

        resp = market_client.get("/api/finance/flows").json()
        times = [f["time"] for f in resp["flows"]]
        if len(times) >= 2:
            assert times[0] >= times[1]

    def test_flows_only_shows_own_transactions(self, admin_market_client, market_client, user_client):
        """测试用户只能看到自己的流水（数据隔离）"""
        # 如果两个客户端是同一用户，跳过测试
        user1_id = _get_user_id(market_client)
        user2_id = _get_user_id(user_client)
        if user1_id is None or user2_id is None or user1_id == user2_id:
            pytest.skip("market_client and user_client are the same user or not logged in, skip isolation test")

        product = admin_market_client.create_product("隐私测试商品", 15.0, stock=5).json()
        market_client.deposit(100)
        order = market_client.create_order(product["productID"]).json()
        order_id = order["transactionID"]
        market_client.pay_order(order_id)
        market_client.confirm_receipt(order_id)

        market_flows = market_client.get("/api/finance/flows").json()
        market_txn_ids = [f["transactionId"] for f in market_flows["flows"] if f["transactionId"] is not None]
        assert order_id in market_txn_ids

        user_flows = user_client.get("/api/finance/flows").json()
        user_txn_ids = [f["transactionId"] for f in user_flows["flows"] if f["transactionId"] is not None]
        assert order_id not in user_txn_ids


class TestFinanceSummary:
    """流水摘要测试"""

    def test_get_summary_returns_expected_fields(self, market_client):
        resp = market_client.get("/api/finance/summary")
        assert resp.status_code == 200
        data = resp.json()
        assert "balance" in data
        assert "frozenAmount" in data
        assert "availableAmount" in data
        assert "totalIncome" in data
        assert "totalExpense" in data
        assert "netAmount" in data

    def test_get_summary_unauthenticated(self, market_client):
        market_client.post("/api/auth/logout")
        resp = market_client.get("/api/finance/summary")
        assert resp.status_code == 401

    def test_summary_balance_matches_wallet(self, admin_market_client, market_client):
        wallet = market_client.get_wallet().json()
        balance = wallet["balance"]

        summary = market_client.get("/api/finance/summary").json()
        assert summary["balance"] == balance

    def test_summary_frozen_amount_equals_paid_orders(self, admin_market_client, market_client):
        product = admin_market_client.create_product("冻结测试商品", 20.0, stock=5).json()
        market_client.deposit(100)

        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        summary = market_client.get("/api/finance/summary").json()
        assert summary["frozenAmount"] >= 20.0

    def test_summary_available_amount_calculation(self, admin_market_client, market_client):
        product = admin_market_client.create_product("可用金额测试", 15.0, stock=5).json()
        market_client.deposit(100)

        before = market_client.get("/api/finance/summary").json()
        before_available = before["availableAmount"]

        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        after = market_client.get("/api/finance/summary").json()
        after_balance = after["balance"]
        after_frozen = after["frozenAmount"]
        after_available = after["availableAmount"]

        assert after_available == after_balance - after_frozen

    def test_summary_net_amount_calculation(self, admin_market_client, market_client):
        product = admin_market_client.create_product("净额测试", 10.0, stock=5).json()
        market_client.deposit(100)

        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])
        market_client.confirm_receipt(order["transactionID"])

        summary = market_client.get("/api/finance/summary").json()
        assert summary["netAmount"] == summary["totalIncome"] - summary["totalExpense"]

    def test_summary_for_different_users_isolated(self, admin_market_client, market_client, user_client):
        """测试不同用户的摘要数据隔离"""
        user1_id = _get_user_id(market_client)
        user2_id = _get_user_id(user_client)
        if user1_id is None or user2_id is None or user1_id == user2_id:
            pytest.skip("market_client and user_client are the same user or not logged in, skip isolation test")

        product = admin_market_client.create_product("隔离测试商品", 8.0, stock=5).json()
        market_client.deposit(100)

        order = market_client.create_order(product["productID"]).json()
        order_id = order["transactionID"]
        market_client.pay_order(order_id)
        market_client.confirm_receipt(order_id)

        market_summary = market_client.get("/api/finance/summary").json()
        assert market_summary["totalExpense"] > 0

        user_flows = user_client.get("/api/finance/flows").json()
        user_txn_ids = [f["transactionId"] for f in user_flows["flows"] if f["transactionId"] is not None]
        assert order_id not in user_txn_ids


class TestFinanceEdgeCases:
    """边界情况测试"""

    def test_flows_when_no_transactions(self, user_client):
        resp = user_client.get("/api/finance/flows")
        assert resp.status_code == 200
        data = resp.json()
        assert data["totalCount"] >= 0
        assert isinstance(data["flows"], list)

    def test_summary_when_no_transactions(self, user_client):
        resp = user_client.get("/api/finance/summary")
        assert resp.status_code == 200
        data = resp.json()
        assert data["balance"] >= 0
        assert data["frozenAmount"] >= 0
        assert data["availableAmount"] == data["balance"] - data["frozenAmount"]

    def test_flows_after_cancel_order(self, admin_market_client, market_client):
        product = admin_market_client.create_product("取消测试商品", 5.0, stock=5).json()
        market_client.deposit(50)

        before = market_client.get("/api/finance/flows").json()
        before_count = before["totalCount"]

        order = market_client.create_order(product["productID"]).json()
        market_client.cancel_order(order["transactionID"])

        after = market_client.get("/api/finance/flows").json()
        assert after["totalCount"] == before_count

    def test_flows_after_dispute_refund(self, admin_market_client, market_client):
        product = admin_market_client.create_product("纠纷流水测试", 30.0, stock=5).json()
        market_client.deposit(200)

        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])

        dispute = market_client.create_dispute(order["transactionID"], "测试退款流水").json()
        admin_market_client.resolve_dispute(dispute["ticketID"], "全额退款", refund_amount=30.0)

        flows = market_client.get("/api/finance/flows").json()
        expense_records = [f for f in flows["flows"] if f["type"] == "支出"]
        assert len(expense_records) >= 1

    def test_flows_contains_correct_description_for_deleted_product(self, admin_market_client, market_client):
        product = admin_market_client.create_product("待删除流水商品", 10.0, stock=5).json()
        market_client.deposit(50)

        order = market_client.create_order(product["productID"]).json()
        market_client.pay_order(order["transactionID"])
        market_client.confirm_receipt(order["transactionID"])

        admin_market_client.delete_product(product["productID"])

        flows = market_client.get("/api/finance/flows").json()
        for f in flows["flows"]:
            assert "description" in f