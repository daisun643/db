import time

import pytest

from api.forum import ForumAPI
from api.market import MarketAPI
from api.notifications import NotificationAPI
from config import TEST_USERS


def unique(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}"


def items(resp):
    data = resp.json()
    if isinstance(data, dict) and "items" in data:
        return data["items"]
    return data


def find_by_title(client: NotificationAPI, title: str, **params):
    resp = client.get_notifications(page=1, pageSize=100, **params)
    assert resp.status_code == 200
    return [item for item in items(resp) if item["title"] == title]


@pytest.fixture
def notification_client():
    c = NotificationAPI()
    c.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
    yield c
    c.close()


@pytest.fixture
def admin_notification_client():
    c = NotificationAPI()
    c.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
    yield c
    c.close()


@pytest.fixture
def manager_notification_client():
    c = NotificationAPI()
    c.login(TEST_USERS["manager"]["email"], TEST_USERS["manager"]["password"])
    yield c
    c.close()


class TestNotificationCenter:
    def test_unauthenticated_cannot_list_notifications(self):
        client = NotificationAPI()
        resp = client.get_notifications()
        client.close()
        assert resp.status_code == 401

    def test_query_mark_read_and_read_all(self, admin_notification_client, notification_client):
        title = unique("系统通知v1")
        create_resp = admin_notification_client.create_system_notification(title, "通知中心基础能力测试", [4])
        assert create_resp.status_code == 200

        unread_before = notification_client.get_unread_count()
        assert unread_before.status_code == 200
        assert unread_before.json()["count"] >= 1

        found = find_by_title(notification_client, title, type="System", isRead="false")
        assert len(found) == 1
        notification = found[0]
        assert notification["type"] == "System"
        assert notification["isRead"] is False

        read_resp = notification_client.mark_read(notification["notificationID"])
        assert read_resp.status_code == 200

        found_after = find_by_title(notification_client, title, type="System", isRead="true")
        assert len(found_after) == 1
        assert found_after[0]["isRead"] is True

        create_resp = admin_notification_client.create_system_notification(unique("全部已读v1"), "全部已读测试", [4])
        assert create_resp.status_code == 200
        all_read_resp = notification_client.mark_all_read()
        assert all_read_resp.status_code == 200
        unread_after = notification_client.get_unread_count()
        assert unread_after.status_code == 200
        assert unread_after.json()["count"] == 0

    def test_user_cannot_operate_others_notification(self, admin_notification_client, notification_client):
        title = unique("隔离测试v1")
        create_resp = admin_notification_client.create_system_notification(title, "权限隔离测试", [4])
        assert create_resp.status_code == 200
        notification = find_by_title(notification_client, title, type="System")[0]

        admin_read = admin_notification_client.mark_read(notification["notificationID"])
        admin_delete = admin_notification_client.delete_notification(notification["notificationID"])
        assert admin_read.status_code == 404
        assert admin_delete.status_code == 404

    def test_type_filter_and_pagination(self, admin_notification_client, notification_client):
        title = unique("分页筛选v1")
        create_resp = admin_notification_client.create_system_notification(title, "分页筛选测试", [4])
        assert create_resp.status_code == 200

        resp = notification_client.get_notifications(type="System", page=1, pageSize=1)
        assert resp.status_code == 200
        data = resp.json()
        assert data["page"] == 1
        assert data["pageSize"] == 1
        assert len(data["items"]) == 1
        assert data["total"] >= 1
        assert all(item["type"] == "System" for item in data["items"])


class TestNotificationEvents:
    def test_forum_mention_event_creates_notification(self, admin_notification_client, notification_client):
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        title = unique("mention-post-v1")
        before = notification_client.get_unread_count().json()["count"]

        post_resp = forum.create_post(1, title, "hello @USER000004 from notification test")
        forum.close()
        assert post_resp.status_code == 201

        found = find_by_title(notification_client, "帖子提及", type="Mention", isRead="false")
        assert any(title in item["content"] for item in found)
        after = notification_client.get_unread_count().json()["count"]
        assert after >= before + 1

    def test_transaction_event_creates_notification(self, manager_notification_client):
        admin_market = MarketAPI()
        manager_market = MarketAPI()
        admin_market.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        manager_market.login(TEST_USERS["manager"]["email"], TEST_USERS["manager"]["password"])

        product_resp = admin_market.create_product(unique("notification-product"), 9.9, stock=3)
        assert product_resp.status_code in (200, 201)
        product_id = product_resp.json()["productID"]

        order_resp = manager_market.create_order(product_id)
        admin_market.close()
        manager_market.close()
        assert order_resp.status_code == 200
        transaction_id = order_resp.json()["transactionID"]

        resp = manager_notification_client.get_notifications(type="Transaction", page=1, pageSize=20)
        assert resp.status_code == 200
        assert any(item["transactionID"] == transaction_id for item in items(resp))

    def test_audit_event_creates_notification(self, admin_notification_client):
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        title = unique("audit-spam-v1")
        post_resp = forum.create_post(1, title, "spam content for audit notification")
        forum.close()
        assert post_resp.status_code == 201
        post_id = post_resp.json()["postID"]

        audits_resp = admin_notification_client.get_pending_audits()
        assert audits_resp.status_code == 200
        audit = next(item for item in audits_resp.json() if item["targetType"] == "Post" and item["targetID"] == post_id)

        reject_resp = admin_notification_client.reject_audit(audit["auditID"])
        assert reject_resp.status_code == 200

        found = find_by_title(admin_notification_client, "帖子审核未通过", type="Audit")
        assert any(item["targetID"] == post_id for item in found)

