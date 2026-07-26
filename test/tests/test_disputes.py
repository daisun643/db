import uuid

import pytest


def _unique(name: str) -> str:
    return f"{name}-{uuid.uuid4().hex[:8]}"


def _login_profiles(*clients):
    return {client: client.get_profile().json()["userId"] for client in clients}


def _client_by_user_id(user_id: int, clients: dict):
    for client, profile_user_id in clients.items():
        if profile_user_id == user_id:
            return client
    raise AssertionError(f"没有找到 userId={user_id} 对应的测试客户端")


def _non_arbitrator_client(dispute: dict, clients: dict, forbidden_ids: set[int]):
    for client, user_id in clients.items():
        if user_id != dispute["arbitratorID"] and user_id not in forbidden_ids:
            return client
    for client, user_id in clients.items():
        if user_id != dispute["arbitratorID"]:
            return client
    raise AssertionError("没有可用的非分配仲裁员客户端")


def _create_paid_order(seller_client, buyer_client, price: float = 50.0):
    create_resp = seller_client.create_product(_unique("纠纷测试商品"), price, stock=1)
    assert create_resp.status_code in (200, 201), create_resp.text
    product = create_resp.json()

    deposit_resp = buyer_client.deposit(price + 100)
    assert deposit_resp.status_code == 200, deposit_resp.text

    order_resp = buyer_client.create_order(product["productID"])
    assert order_resp.status_code == 200, order_resp.text
    order = order_resp.json()

    pay = buyer_client.pay_order(order["transactionID"])
    assert pay.status_code == 200, pay.text
    return product, order


def _create_paid_order_and_dispute(seller_client, buyer_client, reason="测试纠纷", price: float = 50.0):
    product, order = _create_paid_order(seller_client, buyer_client, price=price)
    resp = buyer_client.create_dispute(order["transactionID"], reason)
    assert resp.status_code == 200, resp.text
    return product, order, resp.json()


class TestDisputeCreateAndAssign:
    def test_buyer_create_dispute_success(self, admin_market_client, market_client):
        _, order = _create_paid_order(admin_market_client, market_client, price=25.0)

        resp = market_client.create_dispute(order["transactionID"], "商品质量有问题")

        assert resp.status_code == 200, resp.text
        data = resp.json()
        assert data["reason"] == "商品质量有问题"
        assert data["status"] == "Open"
        assert data["orderStatus"] == "Disputed"
        assert data["transactionID"] == order["transactionID"]

    def test_seller_create_dispute_success(self, admin_market_client, market_client):
        _, order = _create_paid_order(admin_market_client, market_client, price=26.0)

        resp = admin_market_client.create_dispute(order["transactionID"], "买家拒绝沟通")

        assert resp.status_code == 200, resp.text
        data = resp.json()
        assert data["status"] == "Open"
        assert data["userID"] != data["buyerID"]
        assert data["transactionID"] == order["transactionID"]

    def test_unrelated_user_create_dispute_forbidden(self, admin_market_client, market_client, manager_market_client):
        _, order = _create_paid_order(admin_market_client, market_client, price=27.0)

        resp = manager_market_client.create_dispute(order["transactionID"], "我不是买卖双方")

        assert resp.status_code == 403

    def test_duplicate_in_progress_dispute_failed(self, admin_market_client, market_client):
        _, order = _create_paid_order(admin_market_client, market_client, price=28.0)
        first = market_client.create_dispute(order["transactionID"], "第一次纠纷")
        assert first.status_code == 200, first.text

        second = admin_market_client.create_dispute(order["transactionID"], "重复纠纷")

        assert second.status_code == 400

    def test_create_dispute_changes_order_to_disputed(self, admin_market_client, market_client):
        _, order = _create_paid_order(admin_market_client, market_client, price=29.0)

        resp = market_client.create_dispute(order["transactionID"], "状态流转测试")
        assert resp.status_code == 200, resp.text

        orders = market_client.get_my_orders().json()
        target = next(o for o in orders if o["transactionID"] == order["transactionID"])
        assert target["transactionStatus"] == "Disputed"

    def test_system_assigns_arbitrator_and_excludes_buyer_seller(self, admin_market_client, manager_market_client, moderator_market_client, market_client):
        profiles = _login_profiles(admin_market_client, manager_market_client, moderator_market_client, market_client)
        _, _, dispute = _create_paid_order_and_dispute(admin_market_client, market_client, reason="分配测试", price=30.0)

        assert dispute["arbitratorID"] is not None
        assert dispute["assignTime"] is not None
        assert dispute["arbitratorID"] not in {dispute["buyerID"], dispute["sellerID"]}
        assert dispute["arbitratorID"] == profiles[moderator_market_client]
        assert dispute["arbitratorID"] != profiles[manager_market_client]


class TestDisputeResolve:
    def test_non_assigned_arbitrator_cannot_resolve(self, admin_market_client, manager_market_client, moderator_market_client, market_client):
        profiles = _login_profiles(admin_market_client, manager_market_client, moderator_market_client, market_client)
        _, _, dispute = _create_paid_order_and_dispute(admin_market_client, market_client, reason="权限测试", price=31.0)
        non_arbitrator = _non_arbitrator_client(dispute, profiles, {dispute["buyerID"], dispute["sellerID"]})

        resp = non_arbitrator.resolve_dispute(dispute["ticketID"], "非分配仲裁员处理", refund_amount=0)

        assert resp.status_code == 403

    def test_assigned_arbitrator_can_request_supplement(self, admin_market_client, manager_market_client, moderator_market_client, market_client):
        profiles = _login_profiles(admin_market_client, manager_market_client, moderator_market_client, market_client)
        _, order, dispute = _create_paid_order_and_dispute(admin_market_client, market_client, reason="补充材料测试", price=32.0)
        arbitrator = _client_by_user_id(dispute["arbitratorID"], profiles)

        resp = arbitrator.request_dispute_supplement(dispute["ticketID"], "请上传聊天记录和商品照片")

        assert resp.status_code == 200, resp.text
        assert resp.json()["status"] == "NeedSupplement"
        duplicate = market_client.create_dispute(order["transactionID"], "补充期间重复创建")
        assert duplicate.status_code == 400

    def test_assigned_arbitrator_resolve_success(self, admin_market_client, manager_market_client, moderator_market_client, market_client):
        profiles = _login_profiles(admin_market_client, manager_market_client, moderator_market_client, market_client)
        _, _, dispute = _create_paid_order_and_dispute(admin_market_client, market_client, reason="仲裁成功测试", price=33.0)
        arbitrator = _client_by_user_id(dispute["arbitratorID"], profiles)

        resp = arbitrator.resolve_dispute(dispute["ticketID"], "部分质量问题，部分退款", refund_amount=13.0)

        assert resp.status_code == 200, resp.text
        data = resp.json()
        assert data["status"] == "Resolved"
        assert data["orderStatus"] == "Refunded"
        assert data["buyerRefundAmount"] == pytest.approx(13.0)
        assert data["sellerSettlementAmount"] == pytest.approx(20.0)

    def test_partial_refund_wallet_amounts_correct(self, admin_market_client, manager_market_client, moderator_market_client, market_client):
        profiles = _login_profiles(admin_market_client, manager_market_client, moderator_market_client, market_client)
        _, _, dispute = _create_paid_order_and_dispute(admin_market_client, market_client, reason="金额拆分测试", price=40.0)
        arbitrator = _client_by_user_id(dispute["arbitratorID"], profiles)
        buyer_before = market_client.get_wallet().json()["balance"]
        seller_before = admin_market_client.get_wallet().json()["balance"]

        resp = arbitrator.resolve_dispute(dispute["ticketID"], "部分退款", refund_amount=16.0)

        assert resp.status_code == 200, resp.text
        buyer_after = market_client.get_wallet().json()["balance"]
        seller_after = admin_market_client.get_wallet().json()["balance"]
        assert buyer_after == pytest.approx(buyer_before + 16.0)
        assert seller_after == pytest.approx(seller_before + 24.0)
        assert 16.0 + 24.0 == pytest.approx(40.0)

    def test_resolved_order_status_correct(self, admin_market_client, manager_market_client, moderator_market_client, market_client):
        profiles = _login_profiles(admin_market_client, manager_market_client, moderator_market_client, market_client)
        _, order, dispute = _create_paid_order_and_dispute(admin_market_client, market_client, reason="订单完成状态测试", price=35.0)
        arbitrator = _client_by_user_id(dispute["arbitratorID"], profiles)

        resp = arbitrator.resolve_dispute(dispute["ticketID"], "不退款，交易完成", refund_amount=0)
        assert resp.status_code == 200, resp.text

        orders = market_client.get_my_orders().json()
        target = next(o for o in orders if o["transactionID"] == order["transactionID"])
        assert target["transactionStatus"] == "Completed"

    def test_responsible_party_credit_deducted(self, admin_market_client, manager_market_client, moderator_market_client, market_client):
        profiles = _login_profiles(admin_market_client, manager_market_client, moderator_market_client, market_client)
        _, _, dispute = _create_paid_order_and_dispute(admin_market_client, market_client, reason="信用分扣减测试", price=36.0)
        arbitrator = _client_by_user_id(dispute["arbitratorID"], profiles)
        seller_before = admin_market_client.get_profile().json()["credit"]

        resp = arbitrator.resolve_dispute(
            dispute["ticketID"],
            "卖家责任，全额退款",
            refund_amount=36.0,
            responsibility_party="Seller",
        )

        assert resp.status_code == 200, resp.text
        seller_after = admin_market_client.get_profile().json()["credit"]
        assert seller_after == seller_before - 20
        adjustments = admin_market_client.get_credit_adjustments().json()
        assert any("纠纷仲裁" in item["description"] and item["changePoints"] < 0 for item in adjustments)

    def test_buyer_and_seller_receive_notifications(self, admin_market_client, manager_market_client, moderator_market_client, market_client):
        profiles = _login_profiles(admin_market_client, manager_market_client, moderator_market_client, market_client)
        _, _, dispute = _create_paid_order_and_dispute(admin_market_client, market_client, reason="通知测试", price=37.0)
        arbitrator = _client_by_user_id(dispute["arbitratorID"], profiles)

        resp = arbitrator.resolve_dispute(dispute["ticketID"], "通知测试完成", refund_amount=10.0)
        assert resp.status_code == 200, resp.text

        buyer_notifications = market_client.get_notifications().json()
        seller_notifications = admin_market_client.get_notifications().json()
        assert any("纠纷处理完成" in n["title"] for n in buyer_notifications)
        assert any("纠纷处理完成" in n["title"] for n in seller_notifications)
