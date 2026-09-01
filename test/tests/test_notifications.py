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


class TestReplyEvents:
    def test_post_comment_creates_reply_notification(self, admin_notification_client):
        """用户评论别人帖子后，帖子作者收到 Reply 通知"""
        # admin 发帖，user 4 评论 → admin 收到通知
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        title = unique("reply-post-v2")
        post_resp = forum.create_post(1, title, "post for reply test")
        assert post_resp.status_code in (200, 201), post_resp.text
        post_id = post_resp.json()["postID"]
        forum.close()

        user_forum = ForumAPI()
        user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
        comment_resp = user_forum.create_comment(post_id, "test reply comment")
        user_forum.close()
        assert comment_resp.status_code in (200, 201)

        found = find_by_title(admin_notification_client, "帖子新评论", type="Reply", isRead="false")
        assert any(item["targetType"] == "Post" and item["targetID"] == post_id for item in found)

    def test_comment_reply_creates_parent_comment_notification(self, notification_client):
        """用户回复别人评论后，父评论作者收到 Reply 通知"""
        # admin 发帖，user 4 评论，manager 回复 user 4 → user 4 收到通知
        admin_forum = ForumAPI()
        admin_forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        title = unique("reply-comment-v2")
        post_resp = admin_forum.create_post(1, title, "post for nested reply")
        assert post_resp.status_code in (200, 201), post_resp.text
        post_id = post_resp.json()["postID"]
        admin_forum.close()

        user_forum = ForumAPI()
        user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
        comment_resp = user_forum.create_comment(post_id, "parent comment from user")
        assert comment_resp.status_code in (200, 201), comment_resp.text
        parent_comment_id = comment_resp.json()["commentID"]
        user_forum.close()

        manager_forum = ForumAPI()
        manager_forum.login(TEST_USERS["manager"]["email"], TEST_USERS["manager"]["password"])
        reply_resp = manager_forum.create_comment(post_id, "reply from manager", parent_comment_id)
        manager_forum.close()
        assert reply_resp.status_code in (200, 201)

        found = find_by_title(notification_client, "评论回复", type="Reply", isRead="false")
        assert any(item["targetType"] == "Comment" and item["targetID"] == parent_comment_id for item in found)

    def test_self_comment_does_not_notify_self(self, admin_notification_client):
        """自己评论自己的帖子不产生 Reply 通知"""
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        title = unique("self-comment-v2")
        post_resp = forum.create_post(1, title, "self comment test")
        assert post_resp.status_code in (200, 201), post_resp.text
        post_id = post_resp.json()["postID"]

        before = admin_notification_client.get_unread_count().json()["count"]
        forum.create_comment(post_id, "my own comment on my own post")
        forum.close()
        after = admin_notification_client.get_unread_count().json()["count"]

        found = find_by_title(admin_notification_client, "帖子新评论", type="Reply")
        assert not any(item["targetID"] == post_id for item in found)


class TestReportEvents:
    def test_report_review_notifies_reporter(self, notification_client, admin_notification_client):
        """举报处理后，举报人收到 Report 通知"""
        # admin 发帖，user 举报
        admin_forum = ForumAPI()
        admin_forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        title = unique("report-review-v2")
        post_resp = admin_forum.create_post(1, title, "post to be reported")
        assert post_resp.status_code in (200, 201), post_resp.text
        post_id = post_resp.json()["postID"]
        admin_forum.close()

        # user 4 举报
        user_forum = ForumAPI()
        user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
        report_resp = user_forum.create_report("Post", post_id, "test report reason")
        assert report_resp.status_code == 200, report_resp.text
        report_id = report_resp.json()["reportID"]
        user_forum.close()

        # admin 审核拒绝
        review_resp = admin_notification_client.review_report(report_id, "reject", "test result")
        assert review_resp.status_code == 200

        # user 4 (举报人) 收到处理结果通知，包含 targetType/targetID
        found = find_by_title(notification_client, "举报处理结果", type="Report")
        assert len(found) >= 1
        assert any(item["targetType"] == "Post" and item["targetID"] == post_id for item in found)

    def test_report_approval_notifies_penalized_user(self, notification_client, admin_notification_client):
        """举报成立后，被处理用户（帖子作者）收到违规通知"""
        # admin 发帖（admin 有发帖权限）
        admin_forum = ForumAPI()
        admin_forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        title = unique("report-penalty-v2")
        post_resp = admin_forum.create_post(1, title, "post that will be penalized")
        assert post_resp.status_code in (200, 201), post_resp.text
        post_id = post_resp.json()["postID"]

        # user 4 举报
        user_forum = ForumAPI()
        user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
        report_resp = user_forum.create_report("Post", post_id, "test penalty report")
        assert report_resp.status_code == 200, report_resp.text
        report_id = report_resp.json()["reportID"]
        user_forum.close()
        admin_forum.close()

        # admin 审核通过 → 帖子作者(admin)收到违规通知
        review_resp = admin_notification_client.review_report(report_id, "approve", "violation")
        assert review_resp.status_code == 200

        found = find_by_title(admin_notification_client, "违规处理通知", type="Report")
        assert any("举报成立" in item["content"] and item["targetType"] == "Post" and item["targetID"] == post_id for item in found)


class TestDisputeEvents:
    def test_dispute_create_notifies_seller(self, admin_notification_client):
        """发起纠纷后，卖家收到 Dispute 通知（验证本次 tx_id）"""
        admin_market = MarketAPI()
        manager_market = MarketAPI()
        admin_market.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        manager_market.login(TEST_USERS["manager"]["email"], TEST_USERS["manager"]["password"])

        manager_market.deposit(500)
        product_resp = admin_market.create_product(unique("dispute-product-v2"), 50.0, stock=5)
        assert product_resp.status_code in (200, 201), product_resp.text
        product = product_resp.json()
        order_resp = manager_market.create_order(product["productID"])
        assert order_resp.status_code == 200, order_resp.text
        order = order_resp.json()
        tx_id = order["transactionID"]
        manager_market.pay_order(tx_id)

        dispute_resp = manager_market.create_dispute(tx_id, "test dispute reason")
        admin_market.close()
        manager_market.close()
        assert dispute_resp.status_code == 200

        resp = admin_notification_client.get_notifications(type="Dispute", page=1, pageSize=50)
        assert resp.status_code == 200
        data = resp.json()
        dispute_items = data.get("items", data)
        assert len(dispute_items) >= 1
        assert any(
            item.get("targetType") == "Transaction" and item.get("targetID") == tx_id
            for item in dispute_items
        )

    def test_dispute_resolve_notifies_buyer_and_seller(self, admin_notification_client, manager_notification_client):
        """纠纷处理完成后，买卖双方收到结果通知"""
        admin_market = MarketAPI()
        manager_market = MarketAPI()
        admin_market.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        manager_market.login(TEST_USERS["manager"]["email"], TEST_USERS["manager"]["password"])

        manager_market.deposit(500)
        product_resp = admin_market.create_product(unique("dispute-resolve-v2"), 50.0, stock=5)
        assert product_resp.status_code in (200, 201), product_resp.text
        product = product_resp.json()
        order_resp = manager_market.create_order(product["productID"])
        assert order_resp.status_code == 200, order_resp.text
        order = order_resp.json()
        tx_id = order["transactionID"]
        manager_market.pay_order(tx_id)

        dispute_resp = manager_market.create_dispute(tx_id, "test resolve dispute")
        assert dispute_resp.status_code == 200, dispute_resp.text
        dispute = dispute_resp.json()
        admin_market.resolve_dispute(dispute["ticketID"], "resolved", 0)
        admin_market.close()
        manager_market.close()

        resp = admin_notification_client.get_notifications(type="Dispute", page=1, pageSize=50)
        assert resp.status_code == 200
        data = resp.json()
        dispute_items = data.get("items", data)
        assert len(dispute_items) >= 1
        assert any(
            item.get("targetType") == "Transaction" and item.get("targetID") == tx_id
            for item in dispute_items
        )


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


class TestSystemAnnouncements:
    def test_unauthenticated_can_list_announcements(self):
        """公开接口无需登录即可访问"""
        client = NotificationAPI()
        resp = client.get_announcements()
        client.close()
        assert resp.status_code == 200
        data = resp.json()
        assert "items" in data
        assert "total" in data
        assert "page" in data
        assert "pageSize" in data

    def test_create_and_list_announcements(self, admin_notification_client):
        """管理员创建系统公告后，公开接口可查询到"""
        title = unique("公告创建v1")
        create_resp = admin_notification_client.create_system_notification(title, "公告内容测试")
        assert create_resp.status_code == 200

        resp = admin_notification_client.get_announcements(page=1, pageSize=50)
        assert resp.status_code == 200
        data = resp.json()
        assert data["total"] >= 1
        matched = [item for item in data["items"] if item["title"] == title]
        assert len(matched) == 1
        announcement = matched[0]
        assert announcement["content"] == "公告内容测试"
        assert announcement["id"] > 0
        assert announcement["date"] is not None

    def test_announcement_has_expected_fields(self, admin_notification_client):
        """公告返回的字段结构符合前端期望"""
        title = unique("公告字段v1")
        create_resp = admin_notification_client.create_system_notification(title, "字段验证内容")
        assert create_resp.status_code == 200

        resp = admin_notification_client.get_announcements(page=1, pageSize=50)
        assert resp.status_code == 200
        matched = [item for item in resp.json()["items"] if item["title"] == title]
        assert len(matched) == 1
        announcement = matched[0]
        for field in ["id", "title", "content", "date"]:
            assert field in announcement, f"Missing field: {field}"

    def test_announcements_ordered_by_create_time_desc(self, admin_notification_client):
        """公告按创建时间倒序排列"""
        title_a = unique("公告排序A")
        title_b = unique("公告排序B")
        admin_notification_client.create_system_notification(title_a, "先创建")
        admin_notification_client.create_system_notification(title_b, "后创建")

        resp = admin_notification_client.get_announcements(page=1, pageSize=50)
        assert resp.status_code == 200
        announcement_list = resp.json()["items"]
        titles = [item["title"] for item in announcement_list]
        idx_a = titles.index(title_a)
        idx_b = titles.index(title_b)
        assert idx_b < idx_a, "后创建的公告应排在前面"

    def test_announcements_pagination(self, admin_notification_client):
        """公告支持分页参数"""
        for i in range(3):
            admin_notification_client.create_system_notification(unique(f"公告分页{i}"), f"内容{i}")

        resp = admin_notification_client.get_announcements(page=1, pageSize=2)
        assert resp.status_code == 200
        data = resp.json()
        assert data["page"] == 1
        assert data["pageSize"] == 2
        assert len(data["items"]) <= 2
        assert data["total"] >= 3

    def test_announcements_only_include_system_type(self, admin_notification_client, notification_client):
        """公告接口只返回 Type=System 且 TargetType=System 的通知，不包含普通通知"""
        # 创建一条系统公告（TargetType=System）
        title = unique("公告类型v1")
        create_resp = admin_notification_client.create_system_notification(title, "系统公告内容")
        assert create_resp.status_code == 200

        # 公开接口应能查到
        resp = admin_notification_client.get_announcements(page=1, pageSize=50)
        assert resp.status_code == 200
        matched = [item for item in resp.json()["items"] if item["title"] == title]
        assert len(matched) == 1

        # 用户普通通知列表中也存在该通知（作为 System 类型通知发给用户）
        user_resp = notification_client.get_notifications(type="System", page=1, pageSize=100)
        assert user_resp.status_code == 200


def create_post_for_test(forum: ForumAPI, title: str, content: str, forum_id: int = 1) -> int:
    """创建测试帖子并返回 postID"""
    resp = forum.create_post(forum_id, title, content)
    assert resp.status_code in (200, 201), resp.text
    return resp.json()["postID"]


def find_by_target(client: NotificationAPI, type_: str, target_id: int):
    """按类型与目标 ID 过滤当前用户的通知"""
    resp = client.get_notifications(type=type_, page=1, pageSize=100)
    assert resp.status_code == 200
    return [item for item in items(resp) if item["targetID"] == target_id]


class TestForumLikeNotifications:
    """点赞事件通知（Type=Like）"""

    def test_like_post_notifies_author_with_liker_and_link(self, admin_notification_client):
        """别人点赞帖子后，作者收到 Like 通知，含点赞人用户名与帖子详情页链接"""
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        post_id = None
        try:
            post_id = create_post_for_test(forum, unique("like-notify"), "like notification test")

            user_forum = ForumAPI()
            user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
            like_resp = user_forum.like_post(post_id)
            user_forum.close()
            assert like_resp.status_code == 200, like_resp.text

            matched = find_by_target(admin_notification_client, "Like", post_id)
            assert len(matched) >= 1, "作者未收到点赞通知"
            notification = matched[0]
            assert notification["title"] == "收到点赞"
            assert TEST_USERS["user"]["username"] in notification["content"]
            assert notification["link"] == f"/forums/post/{post_id}"
        finally:
            if post_id:
                forum.delete_post(post_id)
            forum.close()

    def test_unlike_and_relike_does_not_duplicate_notification(self, admin_notification_client):
        """取消点赞后再点赞，EventKey 去重不产生第二条 Like 通知"""
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        post_id = None
        try:
            post_id = create_post_for_test(forum, unique("like-dedupe"), "like dedupe test")

            user_forum = ForumAPI()
            user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
            assert user_forum.like_post(post_id).status_code == 200
            assert user_forum.unlike_post(post_id).status_code == 200
            assert user_forum.like_post(post_id).status_code == 200
            user_forum.close()

            matched = find_by_target(admin_notification_client, "Like", post_id)
            assert len(matched) == 1, f"重复点赞应只产生一条通知，实际 {len(matched)} 条"
        finally:
            if post_id:
                forum.delete_post(post_id)
            forum.close()

    def test_self_like_does_not_notify(self, admin_notification_client):
        """自己点赞自己的帖子不产生 Like 通知"""
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        post_id = None
        try:
            post_id = create_post_for_test(forum, unique("like-self"), "self like test")
            assert forum.like_post(post_id).status_code == 200

            matched = find_by_target(admin_notification_client, "Like", post_id)
            assert not matched, "自己点赞自己的帖子不应产生通知"
        finally:
            if post_id:
                forum.delete_post(post_id)
            forum.close()


class TestPostStatusNotifications:
    """帖子状态变化通知（Type=Moderation）"""

    def test_restore_notifies_author(self, admin_notification_client, notification_client):
        """管理员删除后恢复帖子，作者收到“已被恢复”通知"""
        user_forum = ForumAPI()
        user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
        post_id = None
        try:
            post_id = create_post_for_test(user_forum, unique("restore-notify"), "restore notification test")

            assert admin_notification_client is not None
            admin_forum = ForumAPI()
            admin_forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
            assert admin_forum.change_post_status(post_id, "delete").status_code == 200
            restore_resp = admin_forum.change_post_status(post_id, "restore")
            admin_forum.close()
            assert restore_resp.status_code == 200, restore_resp.text

            matched = find_by_target(notification_client, "Moderation", post_id)
            assert any("已被恢复" in item["content"] for item in matched), matched
            assert any(item["link"] == f"/forums/post/{post_id}" for item in matched)
        finally:
            if post_id:
                user_forum.delete_post(post_id)
            user_forum.close()

    def test_ban_notifies_author(self, admin_notification_client, notification_client):
        """管理员封禁帖子后，作者收到“已被封禁”通知"""
        user_forum = ForumAPI()
        user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
        post_id = None
        try:
            post_id = create_post_for_test(user_forum, unique("ban-notify"), "ban notification test")

            admin_forum = ForumAPI()
            admin_forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
            ban_resp = admin_forum.change_post_status(post_id, "ban")
            admin_forum.close()
            assert ban_resp.status_code == 200, ban_resp.text

            matched = find_by_target(notification_client, "Moderation", post_id)
            assert any("已被封禁" in item["content"] for item in matched), matched
        finally:
            if post_id:
                user_forum.delete_post(post_id)
            user_forum.close()

    def test_owner_status_change_does_not_notify_self(self, admin_notification_client):
        """作者自己删除/恢复自己的帖子不产生 Moderation 通知"""
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        post_id = None
        try:
            post_id = create_post_for_test(forum, unique("status-self"), "owner status change test")
            assert forum.change_post_status(post_id, "delete").status_code == 200
            assert forum.change_post_status(post_id, "restore").status_code == 200

            matched = find_by_target(admin_notification_client, "Moderation", post_id)
            assert not matched, "作者操作自己的帖子不应产生状态变化通知"
        finally:
            if post_id:
                forum.delete_post(post_id)
            forum.close()


class TestCommentNotificationContent:
    """评论/回复通知文案与链接升级验证"""

    def test_comment_notification_contains_commenter_and_link(self, admin_notification_client):
        """评论通知文案含评论人用户名，link 指向帖子详情页"""
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        post_id = None
        try:
            post_id = create_post_for_test(forum, unique("comment-content"), "comment notification content test")

            user_forum = ForumAPI()
            user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
            comment_resp = user_forum.create_comment(post_id, "comment for content test")
            user_forum.close()
            assert comment_resp.status_code in (200, 201), comment_resp.text

            matched = find_by_target(admin_notification_client, "Reply", post_id)
            assert matched, "作者未收到评论通知"
            notification = matched[0]
            assert notification["title"] == "帖子新评论"
            assert TEST_USERS["user"]["username"] in notification["content"]
            assert notification["link"] == f"/forums/post/{post_id}"
        finally:
            if post_id:
                forum.delete_post(post_id)
            forum.close()


class TestReportFiledNotifications:
    """举报受理通知（提交举报时通知版主/管理员）"""

    def test_new_report_notifies_moderator_or_admin(
        self, notification_client, admin_notification_client, manager_notification_client
    ):
        """提交举报后，版主或管理员收到“收到新举报”通知，含举报人用户名与原因；举报人自身不接收"""
        forum = ForumAPI()
        forum.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        post_id = None
        try:
            post_id = create_post_for_test(forum, unique("report-filed"), "report filed notification test")

            user_forum = ForumAPI()
            user_forum.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
            report_resp = user_forum.create_report("Post", post_id, "举报受理通知测试原因")
            user_forum.close()
            assert report_resp.status_code == 200, report_resp.text

            candidates = {
                "admin": admin_notification_client,
                "manager": manager_notification_client,
            }
            hits = {
                name: [n for n in find_by_target(client, "Report", post_id) if n["title"] == "收到新举报"]
                for name, client in candidates.items()
            }
            assert any(hits.values()), f"版主/管理员均未收到新举报通知: {hits}"
            received = next(h for h in hits.values() if h)
            assert TEST_USERS["user"]["username"] in received[0]["content"]
            assert "举报受理通知测试原因" in received[0]["content"]

            # 举报人自身不应收到“收到新举报”通知
            reporter_hits = [
                n for n in find_by_target(notification_client, "Report", post_id)
                if n["title"] == "收到新举报"
            ]
            assert not reporter_hits, "举报人不应收到自己提交的举报受理通知"
        finally:
            if post_id:
                forum.delete_post(post_id)
            forum.close()


class TestSseStream:
    """SSE 实时推送端点：事件帧与实时性由 curl/浏览器实测覆盖，此处验证鉴权与响应元信息。"""

    def test_stream_requires_authentication(self):
        client = NotificationAPI()
        resp = client.open_stream()
        client.close()
        assert resp.status_code == 401

    def test_stream_returns_event_stream(self, notification_client):
        resp = notification_client.open_stream()
        try:
            assert resp.status_code == 200
            assert resp.headers["Content-Type"].startswith("text/event-stream")
            # 服务端握手后立即发送注释帧 ": connected"
            first_chunk = next(resp.iter_content(chunk_size=64)).decode("utf-8")
            assert first_chunk.startswith(": connected")
        finally:
            resp.close()

