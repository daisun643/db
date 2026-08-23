import time

import pytest


class TestCreateReport:

    def test_report_post_success(self, forum_client, admin_forum_client):
        """举报帖子成功"""
        # 先创建一个帖子
        post = admin_forum_client.create_post(1, "需要举报的帖子", "这是违规内容").json()
        resp = forum_client.create_report("Post", post["postID"], "违规内容")
        assert resp.status_code == 200
        data = resp.json()
        assert data["targetType"] == "Post"
        assert data["targetID"] == post["postID"]
        assert data["status"] == "Pending"

    def test_report_product_success(self, admin_market_client, forum_client):
        """举报商品成功"""
        product = admin_market_client.create_product("问题商品", 10.0, stock=5).json()
        resp = forum_client.create_report("Product", product["productID"], "假冒伪劣")
        assert resp.status_code == 200
        data = resp.json()
        assert data["targetType"] == "Product"

    def test_report_self_should_be_prevented(self, forum_client):
        """用户不能举报自己"""
        post = forum_client.create_post(1, "自举报测试", "自举报内容").json()
        resp = forum_client.create_report("Post", post["postID"], "测试自举报")
        assert resp.status_code == 400
        assert "自己" in resp.json().get("message", "")

    def test_report_invalid_target(self, forum_client):
        """举报不存在的对象"""
        resp = forum_client.create_report("Post", 99999, "不存在")
        assert resp.status_code == 404

    def test_duplicate_report_should_be_prevented(self, forum_client, admin_forum_client):
        """同一用户对同一对象重复提交相同举报"""
        post = admin_forum_client.create_post(2, "重复举报测试", "重复内容").json()
        resp1 = forum_client.create_report("Post", post["postID"], "第一次举报")
        assert resp1.status_code == 200

        resp2 = forum_client.create_report("Post", post["postID"], "第一次举报")
        assert resp2.status_code == 400
        assert "重复" in resp2.json().get("message", "")

    def test_report_comment_success(self, forum_client, admin_forum_client):
        """举报评论成功"""
        post = admin_forum_client.create_post(2, "评论举报测试", "评论内容").json()
        comment = admin_forum_client.create_comment(post["postID"], "违规评论内容").json()
        resp = forum_client.create_report("Comment", comment["commentID"], "评论违规")
        assert resp.status_code == 200
        data = resp.json()
        assert data["targetType"] == "Comment"
        assert data["status"] == "Pending"

    def test_report_user_success(self, forum_client):
        """举报用户行为成功"""
        # 举报管理员（user id=1），不是自己；reason 加时间戳避免跨测试运行数据污染
        resp = forum_client.create_report("User", 1, f"用户行为不当-{time.time_ns()}", "多次发布垃圾信息")
        assert resp.status_code == 200
        data = resp.json()
        assert data["targetType"] == "User"
        assert data["status"] == "Pending"

    def test_report_user_self_should_be_prevented(self, forum_client):
        """不能举报自己（用户维度）"""
        my_id = forum_client.get("/api/auth/me").json()["user"]["userId"]
        resp = forum_client.create_report("User", my_id, f"自举报-{time.time_ns()}")
        assert resp.status_code == 400

    def test_report_with_description(self, forum_client, admin_forum_client):
        """举报携带补充说明并持久化"""
        post = admin_forum_client.create_post(2, "补充说明测试", "内容").json()
        resp = forum_client.create_report("Post", post["postID"], "违规内容", description="详细补充说明：多次发送广告")
        assert resp.status_code == 200
        assert resp.json()["description"] == "详细补充说明：多次发送广告"


class TestReviewReport:

    def test_admin_can_approve_report(self, forum_client, admin_forum_client):
        """管理员审核通过举报"""
        post = admin_forum_client.create_post(3, "待审核帖子", "等待审核").json()
        report = forum_client.create_report("Post", post["postID"], "请审核").json()

        resp = admin_forum_client.review_report(report["reportID"], "approve", "违规已确认")
        assert resp.status_code == 200
        data = resp.json()
        assert data["status"] == "Approved"

    def test_admin_can_reject_report(self, forum_client, admin_forum_client):
        """管理员驳回举报"""
        post = admin_forum_client.create_post(4, "被误举报帖子", "正常内容").json()
        report = forum_client.create_report("Post", post["postID"], "误举报").json()

        resp = admin_forum_client.review_report(report["reportID"], "reject", "举报不成立")
        assert resp.status_code == 200
        data = resp.json()
        assert data["status"] == "Rejected"

    def test_normal_user_cannot_review_report(self, forum_client, admin_forum_client):
        """普通用户不能处理举报"""
        post = admin_forum_client.create_post(5, "权限测试帖子", "权限测试").json()
        report = forum_client.create_report("Post", post["postID"], "权限测试").json()

        resp = forum_client.review_report(report["reportID"], "approve", "无权处理")
        assert resp.status_code == 403

    def test_report_approved_content_banned(self, forum_client, admin_forum_client):
        """举报成立后帖子被封禁"""
        post = admin_forum_client.create_post(6, "将被封禁", "违规内容").json()
        report = forum_client.create_report("Post", post["postID"], "内容违规").json()

        admin_forum_client.review_report(report["reportID"], "approve", "确认违规")

        reported_post = admin_forum_client.get_post(post["postID"]).json()
        assert reported_post["status"] == "Banned"


class TestReportList:

    def test_admin_can_list_reports(self, forum_client, admin_forum_client):
        """管理员可查看举报列表"""
        post = admin_forum_client.create_post(7, "列表测试", "列表内容").json()
        forum_client.create_report("Post", post["postID"], "列表测试")

        resp = admin_forum_client.get_reports()
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)
        if len(data) > 0:
            r = data[0]
            assert "reportID" in r
            assert "targetType" in r
            assert "status" in r

    def test_normal_user_cannot_list_reports(self, forum_client):
        """普通用户不能查看举报列表"""
        resp = forum_client.get_reports()
        assert resp.status_code == 403

    def test_get_reports_filter_by_status(self, forum_client, admin_forum_client):
        """按状态筛选举报"""
        post = admin_forum_client.create_post(8, "筛选测试", "筛选内容").json()
        report = forum_client.create_report("Post", post["postID"], "筛选测试").json()

        admin_forum_client.review_report(report["reportID"], "approve", "确认")
        resp = admin_forum_client.get_reports(status="Approved")
        assert resp.status_code == 200
        for r in resp.json():
            assert r["status"] == "Approved"


class TestReportDetail:

    def test_admin_can_view_report_detail_with_target(self, forum_client, admin_forum_client):
        """审核员查看举报详情（含被举报内容快照）"""
        post = admin_forum_client.create_post(9, "详情快照测试", "快照内容").json()
        report = forum_client.create_report("Post", post["postID"], "详情测试").json()

        resp = admin_forum_client.get_report(report["reportID"])
        assert resp.status_code == 200
        data = resp.json()
        assert data["reportID"] == report["reportID"]
        assert data["target"] is not None
        assert data["target"]["targetID"] == post["postID"]
        assert data["target"]["title"] == "详情快照测试"
        assert data["target"]["status"] == "Active"

    def test_normal_user_cannot_view_report_detail(self, forum_client, admin_forum_client):
        """普通用户不能查看举报详情"""
        post = admin_forum_client.create_post(9, "详情权限测试", "内容").json()
        report = forum_client.create_report("Post", post["postID"], "详情权限").json()

        resp = forum_client.get_report(report["reportID"])
        assert resp.status_code == 403

    def test_report_user_approved_credit_deducted(self, forum_client, admin_forum_client):
        """举报用户行为成立后，被举报人信用分扣减"""
        # 举报管理员（user id=1）；reason 加时间戳避免跨测试运行数据污染
        resp = forum_client.create_report("User", 1, f"行为违规-{time.time_ns()}")
        assert resp.status_code == 200
        report = resp.json()

        before = admin_forum_client.get_report(report["reportID"]).json()["target"]["ownerCredit"]

        admin_forum_client.review_report(report["reportID"], "approve", "确认违规")

        after = admin_forum_client.get_report(report["reportID"]).json()["target"]["ownerCredit"]
        assert after == before - 20
