import pytest
from uuid import uuid4


class TestForumList:

    def test_get_forums_returns_list(self, forum_client):
        resp = forum_client.get_forums()
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)
        assert len(data) >= 3

    def test_forum_has_expected_fields(self, forum_client):
        resp = forum_client.get_forums()
        forum = resp.json()[0]
        assert "forumID" in forum
        assert "forumName" in forum
        assert "description" in forum
        assert "status" in forum
        assert "postCount" in forum
        assert "canManage" in forum
        assert "managers" in forum

    def test_get_forum_by_id(self, forum_client):
        forums = forum_client.get_forums().json()
        fid = forums[0]["forumID"]
        resp = forum_client.get_forum(fid)
        assert resp.status_code == 200
        assert resp.json()["forumID"] == fid

    def test_get_nonexistent_forum(self, forum_client):
        resp = forum_client.get_forum(99999)
        assert resp.status_code == 404

    def test_unauthenticated_can_list_forums(self, forum_client):
        forum_client.post("/api/auth/logout")
        resp = forum_client.get_forums()
        assert resp.status_code == 200

    def test_inactive_forum_is_hidden_from_regular_and_anonymous_users(
            self, forum_client, admin_forum_client):
        forum_name = f"隐藏版块-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "仅管理员可见")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            update_resp = admin_forum_client.update_forum(
                forum_id, forum_name, "仅管理员可见", "Inactive"
            )
            assert update_resp.status_code == 200
            assert update_resp.json()["status"] == "Inactive"

            admin_ids = {forum["forumID"] for forum in admin_forum_client.get_forums().json()}
            user_ids = {forum["forumID"] for forum in forum_client.get_forums().json()}
            assert forum_id in admin_ids
            assert forum_id not in user_ids
            assert forum_client.get_forum(forum_id).status_code == 404

            forum_client.post("/api/auth/logout")
            anonymous_ids = {forum["forumID"] for forum in forum_client.get_forums().json()}
            assert forum_id not in anonymous_ids
            assert forum_client.get_forum(forum_id).status_code == 404
        finally:
            admin_forum_client.delete_forum(forum_id)


class TestForumCRUD:

    @staticmethod
    def _unique_name(prefix):
        return f"{prefix}-{uuid4().hex[:8]}"

    def test_admin_can_create_forum(self, admin_forum_client):
        forum_name = self._unique_name("测试论坛")
        resp = admin_forum_client.create_forum(forum_name, "这是一个测试论坛")
        assert resp.status_code == 201
        forum_id = resp.json()["forumID"]

        try:
            data = resp.json()
            assert data["forumName"] == forum_name
            assert data["description"] == "这是一个测试论坛"
            assert data["status"] == "Active"
        finally:
            assert admin_forum_client.delete_forum(forum_id).status_code == 200

    def test_user_can_create_forum(self, forum_client, admin_forum_client):
        forum_name = self._unique_name("用户创建版块")
        resp = forum_client.create_forum(forum_name, "由普通用户创建")
        assert resp.status_code == 201
        forum_id = resp.json()["forumID"]

        try:
            assert resp.json()["forumName"] == forum_name
            assert resp.json()["description"] == "由普通用户创建"
            assert resp.json()["status"] == "Active"
            assert resp.json()["postCount"] == 0
            assert resp.headers["Location"].endswith(f"/api/Forums/{forum_id}")

            detail = forum_client.get_forum(forum_id)
            assert detail.status_code == 200
            assert detail.json()["forumName"] == forum_name
            assert forum_id in {forum["forumID"] for forum in forum_client.get_forums().json()}

            assert forum_client.update_forum(forum_id, "不允许修改", "越权").status_code == 403
            assert forum_client.delete_forum(forum_id).status_code == 403
            assert forum_client.assign_forum_manager(forum_id, 4).status_code == 403
        finally:
            assert admin_forum_client.delete_forum(forum_id).status_code == 200

    @pytest.mark.parametrize("client_fixture", [
        "manager_forum_client",
        "moderator_forum_client",
    ])
    def test_staff_roles_retain_create_forum_permission(
            self, request, client_fixture, admin_forum_client):
        role_client = request.getfixturevalue(client_fixture)
        me = role_client.get("/api/auth/me")
        assert me.status_code == 200
        assert "forums.create" in me.json()["user"]["permissions"]

        resp = role_client.create_forum(
            self._unique_name(f"{client_fixture}创建版块"),
            "管理角色应保留普通用户的创建能力",
        )
        assert resp.status_code == 201
        forum_id = resp.json()["forumID"]
        try:
            assert resp.json()["status"] == "Active"
        finally:
            assert admin_forum_client.delete_forum(forum_id).status_code == 200

    def test_unauthenticated_user_cannot_create_forum(self, forum_client):
        forum_client.post("/api/auth/logout")
        resp = forum_client.create_forum(self._unique_name("未登录版块"), "未登录不应创建")
        assert resp.status_code == 401

    def test_admin_can_update_forum(self, admin_forum_client):
        create_resp = admin_forum_client.create_forum(self._unique_name("待修改论坛"), "原描述")
        fid = create_resp.json()["forumID"]
        updated_name = self._unique_name("修改后论坛")
        resp = admin_forum_client.update_forum(fid, updated_name, "新描述", "Active")
        assert resp.status_code == 200
        assert resp.json()["forumName"] == updated_name
        assert resp.json()["description"] == "新描述"

    def test_user_cannot_update_forum(self, forum_client, admin_forum_client):
        create_resp = admin_forum_client.create_forum(self._unique_name("受保护论坛"), "描述")
        assert create_resp.status_code == 201
        fid = create_resp.json()["forumID"]
        resp = forum_client.update_forum(fid, "恶意修改", "恶意描述")
        assert resp.status_code == 403

    def test_admin_can_delete_forum(self, admin_forum_client):
        create_resp = admin_forum_client.create_forum("待删除论坛", "即将删除")
        fid = create_resp.json()["forumID"]
        resp = admin_forum_client.delete_forum(fid)
        assert resp.status_code == 200

    def test_create_forum_short_name_rejected(self, admin_forum_client):
        resp = admin_forum_client.create_forum("A", "名称太短")
        assert resp.status_code == 400

    @pytest.mark.parametrize("forum_name", ["", " ", "  ", " A "])
    def test_create_forum_rejects_name_invalid_after_trim(self, forum_client, forum_name):
        resp = forum_client.create_forum(forum_name, "修剪后名称不合法")
        assert resp.status_code == 400

    def test_create_forum_accepts_maximum_lengths(self, forum_client, admin_forum_client):
        forum_name = "N" * 100
        resp = forum_client.create_forum(forum_name, "D" * 500)
        assert resp.status_code == 201
        forum_id = resp.json()["forumID"]

        try:
            assert len(resp.json()["forumName"]) == 100
            assert len(resp.json()["description"]) == 500
        finally:
            assert admin_forum_client.delete_forum(forum_id).status_code == 200

    @pytest.mark.parametrize("forum_name,description", [
        ("N" * 101, "名称过长"),
        ("描述过长版块", "D" * 501),
    ])
    def test_create_forum_rejects_oversized_fields(self, forum_client, forum_name, description):
        resp = forum_client.create_forum(forum_name, description)
        assert resp.status_code == 400

    def test_create_forum_trims_name_and_description(self, admin_forum_client):
        forum_name = self._unique_name("去除空格版块")
        resp = admin_forum_client.create_forum(f"  {forum_name}  ", "  版块描述  ")
        assert resp.status_code == 201
        forum_id = resp.json()["forumID"]

        try:
            assert resp.json()["forumName"] == forum_name
            assert resp.json()["description"] == "版块描述"
            assert resp.headers["Location"].endswith(f"/api/Forums/{forum_id}")
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_duplicate_forum_name_rejected(self, admin_forum_client):
        forum_name = self._unique_name("同名版块")
        first = admin_forum_client.create_forum(forum_name, "原版块")
        assert first.status_code == 201
        forum_id = first.json()["forumID"]

        try:
            duplicate = admin_forum_client.create_forum(f" {forum_name} ", "重复版块")
            assert duplicate.status_code == 400
            assert duplicate.json()["message"] == "已存在同名版块"
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_duplicate_forum_name_is_case_insensitive(self, forum_client, admin_forum_client):
        forum_name = f"CaseForum-{uuid4().hex[:8]}"
        first = forum_client.create_forum(forum_name, "原版块")
        assert first.status_code == 201
        forum_id = first.json()["forumID"]

        try:
            duplicate = forum_client.create_forum(forum_name.swapcase(), "大小写重复")
            assert duplicate.status_code == 400
            assert duplicate.json()["message"] == "已存在同名版块"
        finally:
            assert admin_forum_client.delete_forum(forum_id).status_code == 200

    def test_update_forum_to_duplicate_name_rejected(self, admin_forum_client):
        first = admin_forum_client.create_forum(self._unique_name("已有版块"), "描述")
        second = admin_forum_client.create_forum(self._unique_name("待改名版块"), "描述")
        assert first.status_code == 201
        assert second.status_code == 201
        first_id = first.json()["forumID"]
        second_id = second.json()["forumID"]

        try:
            resp = admin_forum_client.update_forum(
                second_id, first.json()["forumName"], "重复名称", "Active"
            )
            assert resp.status_code == 400
            assert resp.json()["message"] == "已存在同名版块"
        finally:
            admin_forum_client.delete_forum(first_id)
            admin_forum_client.delete_forum(second_id)

    @pytest.mark.parametrize("operation", ["update", "delete"])
    def test_nonexistent_forum_mutation_returns_404(self, admin_forum_client, operation):
        if operation == "update":
            resp = admin_forum_client.update_forum(99999, "不存在版块", "描述")
        else:
            resp = admin_forum_client.delete_forum(99999)
        assert resp.status_code == 404

    def test_user_cannot_delete_forum(self, forum_client, admin_forum_client):
        create_resp = admin_forum_client.create_forum(
            self._unique_name("禁止用户删除"), "受保护版块"
        )
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            assert forum_client.delete_forum(forum_id).status_code == 403
            assert admin_forum_client.get_forum(forum_id).status_code == 200
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_forum_with_posts_cannot_be_deleted(self, admin_forum_client):
        forum = next(
            forum for forum in admin_forum_client.get_forums().json()
            if forum["postCount"] > 0
        )
        resp = admin_forum_client.delete_forum(forum["forumID"])
        assert resp.status_code == 400
        assert "仍有帖子" in resp.json()["message"]
        assert admin_forum_client.get_forum(forum["forumID"]).status_code == 200


class TestForumManagers:

    def test_unauthenticated_user_cannot_list_managed_forums(self, forum_client):
        forum_client.post("/api/auth/logout")
        assert forum_client.get_my_forums().status_code == 401

    def test_my_forums_only_returns_assigned_forums(
            self, forum_client, admin_forum_client):
        managed_name = f"我的版块-{uuid4().hex[:8]}"
        other_name = f"其他版块-{uuid4().hex[:8]}"
        managed = admin_forum_client.create_forum(managed_name, "由当前用户管理")
        other = admin_forum_client.create_forum(other_name, "不由当前用户管理")
        assert managed.status_code == 201
        assert other.status_code == 201
        managed_id = managed.json()["forumID"]
        other_id = other.json()["forumID"]

        try:
            assert admin_forum_client.assign_forum_manager(managed_id, 4).status_code == 200
            response = forum_client.get_my_forums()
            assert response.status_code == 200
            forum_ids = {forum["forumID"] for forum in response.json()}
            assert managed_id in forum_ids
            assert other_id not in forum_ids
            assigned = next(f for f in response.json() if f["forumID"] == managed_id)
            assert assigned["canManage"] is True
        finally:
            admin_forum_client.delete_forum(managed_id)
            admin_forum_client.delete_forum(other_id)

    def test_assigned_manager_can_update_own_forum(
            self, forum_client, admin_forum_client):
        forum_name = f"版主管理-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "修改前")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            assert admin_forum_client.assign_forum_manager(forum_id, 4).status_code == 200
            updated_name = f"版主已修改-{uuid4().hex[:8]}"
            response = forum_client.update_forum(
                forum_id, updated_name, "由版主更新", "Inactive"
            )
            assert response.status_code == 200
            assert response.json()["forumName"] == updated_name
            assert response.json()["description"] == "由版主更新"
            assert response.json()["status"] == "Inactive"
            assert response.json()["canManage"] is True
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_admin_can_assign_manager(self, admin_forum_client):
        forums = admin_forum_client.get_forums().json()
        fid = forums[0]["forumID"]
        resp = admin_forum_client.assign_forum_manager(fid, 4)
        assert resp.status_code == 200

        forum = admin_forum_client.get_forum(fid).json()
        manager_ids = [m["userID"] for m in forum["managers"]]
        assert 4 in manager_ids

    def test_admin_can_remove_manager(self, admin_forum_client):
        forums = admin_forum_client.get_forums().json()
        fid = forums[0]["forumID"]
        admin_forum_client.assign_forum_manager(fid, 4)
        resp = admin_forum_client.remove_forum_manager(fid, 4)
        assert resp.status_code == 200

    def test_user_cannot_assign_manager(self, forum_client):
        forums = forum_client.get_forums().json()
        fid = forums[0]["forumID"]
        resp = forum_client.assign_forum_manager(fid, 4)
        assert resp.status_code == 403

    def test_assigned_manager_can_view_inactive_forum(
            self, forum_client, admin_forum_client):
        forum_name = f"版主可见版块-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "版主专用")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            assert admin_forum_client.assign_forum_manager(forum_id, 4).status_code == 200
            update_resp = admin_forum_client.update_forum(
                forum_id, forum_name, "版主专用", "Inactive"
            )
            assert update_resp.status_code == 200

            detail_resp = forum_client.get_forum(forum_id)
            assert detail_resp.status_code == 200
            assert detail_resp.json()["status"] == "Inactive"
            assert forum_id in {
                forum["forumID"] for forum in forum_client.get_forums().json()
            }

            assert admin_forum_client.remove_forum_manager(forum_id, 4).status_code == 200
            assert forum_client.get_forum(forum_id).status_code == 404
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_assigning_same_manager_is_idempotent(self, admin_forum_client):
        forum_name = f"重复指派版主-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "测试重复指派")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            first = admin_forum_client.assign_forum_manager(forum_id, 4)
            second = admin_forum_client.assign_forum_manager(forum_id, 4)
            assert first.status_code == 200
            assert second.status_code == 200
            assert second.json()["message"] == "该用户已经是版主"

            managers = admin_forum_client.get_forum(forum_id).json()["managers"]
            assert [manager["userID"] for manager in managers].count(4) == 1
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_assign_manager_validates_forum_and_user(self, admin_forum_client):
        missing_forum = admin_forum_client.assign_forum_manager(99999, 4)
        assert missing_forum.status_code == 404

        forum_name = f"版主校验版块-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "校验用户")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            missing_user = admin_forum_client.assign_forum_manager(forum_id, 99999)
            assert missing_user.status_code == 400
            assert missing_user.json()["message"] == "用户不存在或不可用"
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_remove_unassigned_manager_returns_404(self, admin_forum_client):
        forum_name = f"未指派版主-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "没有版主")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            resp = admin_forum_client.remove_forum_manager(forum_id, 4)
            assert resp.status_code == 404
        finally:
            admin_forum_client.delete_forum(forum_id)


class TestPostList:

    def test_get_posts_returns_list(self, forum_client):
        resp = forum_client.get_posts()
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)
        assert len(data) > 0

    def test_post_has_expected_fields(self, forum_client):
        resp = forum_client.get_posts()
        post = resp.json()[0]
        for field in ["postID", "title", "contentPreview", "heatScore", "likeCount",
                       "viewCount", "commentCount", "status", "createTime",
                       "userID", "username", "forumID", "forumName", "tags",
                       "imageUrls", "isLiked", "isFavorited"]:
            assert field in post, f"Missing field: {field}"

    def test_filter_posts_by_forum(self, forum_client):
        forums = forum_client.get_forums().json()
        fid = forums[0]["forumID"]
        resp = forum_client.get_posts(forumId=fid)
        assert resp.status_code == 200
        for post in resp.json():
            assert post["forumID"] == fid

    def test_filter_posts_by_keyword(self, forum_client):
        resp = forum_client.get_posts(keyword="图书馆")
        assert resp.status_code == 200
        for post in resp.json():
            assert "图书馆" in post["title"] or "图书馆" in post["contentPreview"]

    def test_filter_posts_by_tag(self, forum_client):
        resp = forum_client.get_posts(tag="教程")
        assert resp.status_code == 200
        for post in resp.json():
            assert "教程" in post["tags"]

    def test_filter_posts_by_multiple_tags(self, forum_client):
        forum_id = forum_client.get_forums().json()[0]["forumID"]
        create_resp = forum_client.create_post(
            forum_id, "标签筛选帖子", "验证多标签筛选", tag_names=["标签A", "标签B"]
        )
        assert create_resp.status_code == 201

        resp = forum_client.get_posts(tags="标签A, 标签B", tagOp="and")
        assert resp.status_code == 200
        post_ids = [post["postID"] for post in resp.json()]
        assert create_resp.json()["postID"] in post_ids

    def test_filter_posts_by_multiple_tags_with_or(self, forum_client):
        forum_id = forum_client.get_forums().json()[0]["forumID"]
        first = forum_client.create_post(
            forum_id, "OR 标签筛选帖子甲", "验证 OR 标签筛选", tag_names=["OR标签甲"]
        )
        second = forum_client.create_post(
            forum_id, "OR 标签筛选帖子乙", "验证 OR 标签筛选", tag_names=["OR标签乙"]
        )
        assert first.status_code == 201
        assert second.status_code == 201

        resp = forum_client.get_posts(tags="OR标签甲,OR标签乙", tagOp="or")
        assert resp.status_code == 200
        post_ids = {post["postID"] for post in resp.json()}
        assert {first.json()["postID"], second.json()["postID"]} <= post_ids

    def test_filter_posts_by_heat_range(self, forum_client):
        resp = forum_client.get_posts(minHeat=0, maxHeat=999999)
        assert resp.status_code == 200
        posts = resp.json()
        if posts:
            assert all(isinstance(post["heatScore"], int) for post in posts)

    def test_sort_by_latest(self, forum_client):
        resp = forum_client.get_posts(sort="latest")
        assert resp.status_code == 200
        posts = resp.json()
        if len(posts) >= 2:
            assert posts[0]["createTime"] >= posts[1]["createTime"]

    def test_sort_by_hot(self, forum_client):
        resp = forum_client.get_posts(sort="hot")
        assert resp.status_code == 200

    def test_get_posts_returns_total_count(self, forum_client):
        resp = forum_client.get_posts(page=1, pageSize=1)
        assert resp.status_code == 200
        assert "x-total-count" in {key.lower() for key in resp.headers}
        assert int(resp.headers["X-Total-Count"]) >= len(resp.json())
        assert len(resp.json()) <= 1

    @pytest.mark.parametrize("params", [
        {"sort": "unknown"},
        {"tagOp": "xor"},
        {"from": "2026-07-29T00:00:00", "to": "2026-07-28T00:00:00"},
        {"minHeat": 10, "maxHeat": 9},
        {"minHeat": -1},
        {"maxHeat": -1},
    ])
    def test_invalid_post_filters_are_rejected(self, forum_client, params):
        resp = forum_client.get_posts(**params)
        assert resp.status_code == 400

    def test_status_filter_is_case_insensitive(self, forum_client):
        resp = forum_client.get_posts(status="aCtIvE")
        assert resp.status_code == 200
        assert all(post["status"] == "Active" for post in resp.json())

    def test_unauthenticated_can_list_posts(self, forum_client):
        forum_client.post("/api/auth/logout")
        resp = forum_client.get_posts()
        assert resp.status_code == 200

    def test_get_post_by_id(self, forum_client):
        posts = forum_client.get_posts().json()
        pid = posts[0]["postID"]
        resp = forum_client.get_post(pid)
        assert resp.status_code == 200
        assert resp.json()["postID"] == pid
        assert "content" in resp.json()

    def test_get_nonexistent_post(self, forum_client):
        resp = forum_client.get_post(99999)
        assert resp.status_code == 404

    def test_view_count_increments(self, forum_client):
        posts = forum_client.get_posts().json()
        pid = posts[0]["postID"]
        detail1 = forum_client.get_post(pid).json()
        detail2 = forum_client.get_post(pid).json()
        assert detail2["viewCount"] >= detail1["viewCount"]


class TestPostCRUD:

    def _get_first_forum_id(self, client):
        return client.get_forums().json()[0]["forumID"]

    def test_user_can_create_post(self, forum_client):
        fid = self._get_first_forum_id(forum_client)
        resp = forum_client.create_post(fid, "测试帖子标题", "这是测试帖子的内容")
        assert resp.status_code == 201
        data = resp.json()
        assert data["title"] == "测试帖子标题"
        assert data["content"] == "这是测试帖子的内容"
        assert data["status"] == "Active"

    def test_create_post_preserves_markdown_content(self, forum_client):
        fid = self._get_first_forum_id(forum_client)
        markdown = "## 二级标题\n\n- 条目一\n- **加粗条目**\n\n`inline_code`"
        resp = forum_client.create_post(fid, "Markdown 帖子", markdown)
        assert resp.status_code == 201
        post_id = resp.json()["postID"]

        try:
            assert resp.json()["content"] == markdown
            detail = forum_client.get_post(post_id)
            assert detail.status_code == 200
            assert detail.json()["content"] == markdown
        finally:
            forum_client.delete_post(post_id)

    def test_create_post_with_tags(self, forum_client):
        fid = self._get_first_forum_id(forum_client)
        resp = forum_client.create_post(fid, "带标签的帖子", "内容", tag_names=["分享", "经验"])
        assert resp.status_code == 201
        assert "分享" in resp.json()["tags"]
        assert "经验" in resp.json()["tags"]

    def test_create_post_missing_title_rejected(self, forum_client):
        fid = self._get_first_forum_id(forum_client)
        resp = forum_client.post("/api/posts", json={
            "forumID": fid, "title": "", "content": "内容",
            "tagNames": [], "imageUrls": [],
        })
        assert resp.status_code == 400

    def test_create_post_invalid_forum_rejected(self, forum_client):
        resp = forum_client.create_post(99999, "标题", "内容")
        assert resp.status_code == 400

    def test_user_can_update_own_post(self, forum_client):
        fid = self._get_first_forum_id(forum_client)
        create_resp = forum_client.create_post(fid, "原标题", "原内容")
        pid = create_resp.json()["postID"]
        resp = forum_client.update_post(pid, "新标题", "新内容")
        assert resp.status_code == 200
        assert resp.json()["title"] == "新标题"
        assert resp.json()["content"] == "新内容"

    def test_user_cannot_update_others_post(self, forum_client, admin_forum_client):
        fid = self._get_first_forum_id(admin_forum_client)
        create_resp = admin_forum_client.create_post(fid, "管理员帖子", "内容")
        pid = create_resp.json()["postID"]
        resp = forum_client.update_post(pid, "恶意修改", "恶意内容")
        assert resp.status_code == 403

    def test_user_can_delete_own_post(self, forum_client):
        fid = self._get_first_forum_id(forum_client)
        create_resp = forum_client.create_post(fid, "待删除帖子", "内容")
        pid = create_resp.json()["postID"]
        resp = forum_client.delete_post(pid)
        assert resp.status_code == 200

    def test_get_my_posts(self, forum_client):
        fid = self._get_first_forum_id(forum_client)
        forum_client.create_post(fid, "我的帖子A", "内容A")
        forum_client.create_post(fid, "我的帖子B", "内容B")
        resp = forum_client.get_my_posts()
        assert resp.status_code == 200
        titles = [p["title"] for p in resp.json()]
        assert "我的帖子A" in titles
        assert "我的帖子B" in titles


class TestPostModeration:

    def test_admin_can_pin_post(self, admin_forum_client):
        forums = admin_forum_client.get_forums().json()
        fid = forums[0]["forumID"]
        posts = admin_forum_client.get_posts(forumId=fid).json()
        pid = posts[0]["postID"]
        resp = admin_forum_client.change_post_status(pid, "pin")
        assert resp.status_code == 200
        assert resp.json()["status"] == "Pinned"

    def test_admin_can_unpin_post(self, admin_forum_client):
        forums = admin_forum_client.get_forums().json()
        fid = forums[0]["forumID"]
        posts = admin_forum_client.get_posts(forumId=fid).json()
        pid = posts[0]["postID"]
        admin_forum_client.change_post_status(pid, "pin")
        resp = admin_forum_client.change_post_status(pid, "unpin")
        assert resp.status_code == 200
        assert resp.json()["status"] == "Active"

    def test_admin_can_set_elite(self, admin_forum_client):
        forums = admin_forum_client.get_forums().json()
        pid = admin_forum_client.get_posts(forumId=forums[0]["forumID"]).json()[0]["postID"]
        resp = admin_forum_client.change_post_status(pid, "elite")
        assert resp.status_code == 200
        assert resp.json()["status"] == "Elite"

    def test_user_cannot_pin_post(self, forum_client):
        posts = forum_client.get_posts().json()
        pid = posts[0]["postID"]
        resp = forum_client.change_post_status(pid, "pin")
        assert resp.status_code == 403

    def test_invalid_status_action_rejected(self, admin_forum_client):
        posts = admin_forum_client.get_posts().json()
        pid = posts[0]["postID"]
        resp = admin_forum_client.change_post_status(pid, "invalid_action")
        assert resp.status_code == 400

    def test_sensitive_word_triggers_review(self, forum_client):
        fid = forum_client.get_forums().json()[0]["forumID"]
        resp = forum_client.create_post(fid, "含敏感词的帖子", "这里包含敏感词内容")
        assert resp.status_code == 201
        assert resp.json()["status"] == "PendingReview"


class TestPostLike:

    def test_user_can_like_post(self, forum_client):
        posts = forum_client.get_posts().json()
        pid = posts[0]["postID"]
        forum_client.unlike_post(pid)
        resp = forum_client.like_post(pid)
        assert resp.status_code == 200
        assert resp.json()["liked"] is True
        assert isinstance(resp.json()["likeCount"], int)

    def test_user_can_unlike_post(self, forum_client):
        posts = forum_client.get_posts().json()
        pid = posts[0]["postID"]
        forum_client.like_post(pid)
        resp = forum_client.unlike_post(pid)
        assert resp.status_code == 200
        assert resp.json()["liked"] is False

    def test_double_like_does_not_increment(self, forum_client):
        posts = forum_client.get_posts().json()
        pid = posts[0]["postID"]
        forum_client.unlike_post(pid)
        r1 = forum_client.like_post(pid).json()
        r2 = forum_client.like_post(pid).json()
        assert r1["likeCount"] == r2["likeCount"]

    def test_like_reflected_in_post_detail(self, forum_client):
        posts = forum_client.get_posts().json()
        pid = posts[0]["postID"]
        forum_client.unlike_post(pid)
        forum_client.like_post(pid)
        detail = forum_client.get_post(pid).json()
        assert detail["isLiked"] is True


class TestFavoriteFolders:

    def _create_folder(self, client, name="测试收藏夹"):
        resp = client.create_favorite_folder(name)
        assert resp.status_code == 200
        return resp.json()

    def _get_seed_post_id(self, client):
        return client.get_posts().json()[0]["postID"]

    def test_unauthenticated_cannot_list_favorite_folders(self, forum_client):
        forum_client.post("/api/auth/logout")
        resp = forum_client.get_favorite_folders()
        assert resp.status_code == 401

    def test_user_can_create_and_list_favorite_folder(self, forum_client):
        folder = self._create_folder(forum_client, "接口测试收藏夹")

        resp = forum_client.get_favorite_folders()
        assert resp.status_code == 200
        folders = resp.json()
        folder_ids = [f["folderID"] for f in folders]
        assert folder["folderID"] in folder_ids

        created = next(f for f in folders if f["folderID"] == folder["folderID"])
        assert created["folderName"] == "接口测试收藏夹"
        assert created["postCount"] == 0
        assert "createTime" in created

    def test_create_favorite_folder_trims_name(self, forum_client):
        resp = forum_client.create_favorite_folder("  去空格收藏夹  ")
        assert resp.status_code == 200
        assert resp.json()["folderName"] == "去空格收藏夹"

    def test_create_favorite_folder_empty_name_rejected(self, forum_client):
        resp = forum_client.create_favorite_folder("")
        assert resp.status_code == 400

    def test_user_can_update_favorite_folder(self, forum_client):
        folder = self._create_folder(forum_client, "待改名收藏夹")
        resp = forum_client.update_favorite_folder(folder["folderID"], "改名后收藏夹")
        assert resp.status_code == 200
        assert resp.json()["folderID"] == folder["folderID"]
        assert resp.json()["folderName"] == "改名后收藏夹"

    def test_update_nonexistent_favorite_folder_returns_404(self, forum_client):
        resp = forum_client.update_favorite_folder(99999, "不存在收藏夹")
        assert resp.status_code == 404

    def test_user_can_delete_favorite_folder(self, forum_client):
        folder = self._create_folder(forum_client, "待删除收藏夹")
        resp = forum_client.delete_favorite_folder(folder["folderID"])
        assert resp.status_code == 200

        folders = forum_client.get_favorite_folders().json()
        folder_ids = [f["folderID"] for f in folders]
        assert folder["folderID"] not in folder_ids

    def test_user_can_add_and_list_post_in_favorite_folder(self, forum_client):
        folder = self._create_folder(forum_client, "帖子收藏夹")
        post_id = self._get_seed_post_id(forum_client)

        add_resp = forum_client.add_post_to_favorite_folder(folder["folderID"], post_id)
        assert add_resp.status_code == 200

        posts_resp = forum_client.get_favorite_folder_posts(folder["folderID"])
        assert posts_resp.status_code == 200
        posts = posts_resp.json()
        post_ids = [p["postID"] for p in posts]
        assert post_id in post_ids

        folders = forum_client.get_favorite_folders().json()
        created = next(f for f in folders if f["folderID"] == folder["folderID"])
        assert created["postCount"] == 1

    def test_adding_same_post_twice_keeps_single_favorite(self, forum_client):
        folder = self._create_folder(forum_client, "去重收藏夹")
        post_id = self._get_seed_post_id(forum_client)

        first = forum_client.add_post_to_favorite_folder(folder["folderID"], post_id)
        second = forum_client.add_post_to_favorite_folder(folder["folderID"], post_id)
        assert first.status_code == 200
        assert second.status_code == 200

        posts = forum_client.get_favorite_folder_posts(folder["folderID"]).json()
        assert [p["postID"] for p in posts].count(post_id) == 1

    def test_user_can_remove_post_from_favorite_folder(self, forum_client):
        folder = self._create_folder(forum_client, "移除帖子收藏夹")
        post_id = self._get_seed_post_id(forum_client)
        forum_client.add_post_to_favorite_folder(folder["folderID"], post_id)

        resp = forum_client.remove_post_from_favorite_folder(folder["folderID"], post_id)
        assert resp.status_code == 200

        posts = forum_client.get_favorite_folder_posts(folder["folderID"]).json()
        assert post_id not in [p["postID"] for p in posts]

    def test_add_nonexistent_post_to_favorite_folder_returns_404(self, forum_client):
        folder = self._create_folder(forum_client, "不存在帖子收藏夹")
        resp = forum_client.add_post_to_favorite_folder(folder["folderID"], 99999)
        assert resp.status_code == 404

    def test_users_cannot_access_each_others_favorite_folders(self, forum_client, admin_forum_client):
        admin_folder = self._create_folder(admin_forum_client, "管理员收藏夹")
        admin_post_id = self._get_seed_post_id(admin_forum_client)
        admin_forum_client.add_post_to_favorite_folder(admin_folder["folderID"], admin_post_id)

        folders = forum_client.get_favorite_folders().json()
        assert admin_folder["folderID"] not in [f["folderID"] for f in folders]
        assert forum_client.get_favorite_folder_posts(admin_folder["folderID"]).status_code == 404
        assert forum_client.update_favorite_folder(admin_folder["folderID"], "越权改名").status_code == 404
        assert forum_client.delete_favorite_folder(admin_folder["folderID"]).status_code == 404


class TestComments:

    def _get_seed_post_id(self, client):
        return client.get_posts().json()[0]["postID"]

    def test_get_comments_returns_list(self, forum_client):
        pid = self._get_seed_post_id(forum_client)
        resp = forum_client.get_comments(pid)
        assert resp.status_code == 200
        assert isinstance(resp.json(), list)

    def test_user_can_create_comment(self, forum_client):
        pid = self._get_seed_post_id(forum_client)
        resp = forum_client.create_comment(pid, "这是一条测试评论")
        assert resp.status_code == 200
        data = resp.json()
        assert data["content"] == "这是一条测试评论"
        assert "commentID" in data
        assert "createTime" in data

    def test_user_can_reply_to_comment(self, forum_client):
        pid = self._get_seed_post_id(forum_client)
        parent = forum_client.create_comment(pid, "父评论").json()
        resp = forum_client.create_comment(pid, "回复内容", parent_comment_id=parent["commentID"])
        assert resp.status_code == 200
        assert resp.json()["parentCommentID"] == parent["commentID"]

    def test_comment_appears_in_list(self, forum_client):
        pid = self._get_seed_post_id(forum_client)
        forum_client.create_comment(pid, "独特评论内容xyz123")
        comments = forum_client.get_comments(pid).json()
        contents = [c["content"] for c in comments]
        assert "独特评论内容xyz123" in contents

    def test_user_can_delete_own_comment(self, forum_client):
        pid = self._get_seed_post_id(forum_client)
        comment = forum_client.create_comment(pid, "待删除评论").json()
        resp = forum_client.delete_comment(comment["commentID"])
        assert resp.status_code == 200

    def test_user_cannot_delete_others_comment(self, forum_client, admin_forum_client):
        pid = self._get_seed_post_id(admin_forum_client)
        comment = admin_forum_client.create_comment(pid, "管理员评论").json()
        resp = forum_client.delete_comment(comment["commentID"])
        assert resp.status_code == 403

    def test_comment_empty_content_rejected(self, forum_client):
        pid = self._get_seed_post_id(forum_client)
        resp = forum_client.create_comment(pid, "")
        assert resp.status_code == 400

    def test_get_comments_nonexistent_post(self, forum_client):
        resp = forum_client.get_comments(99999)
        assert resp.status_code == 404


class TestTags:

    def test_get_tags_returns_list(self, forum_client):
        resp = forum_client.get_tags()
        assert resp.status_code == 200
        assert isinstance(resp.json(), list)

    def test_search_tags_by_keyword(self, forum_client):
        resp = forum_client.get_tags(keyword="分享")
        assert resp.status_code == 200
        tags = resp.json()
        assert isinstance(tags, list)
        for tag in tags:
            assert "分享" in tag

    def test_get_tag_stats(self, forum_client):
        resp = forum_client.get_tag_stats(top=5)
        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data, list)
        assert len(data) <= 5
        if data:
            sample = data[0]
            assert "tagID" in sample
            assert "tagName" in sample
            assert "postCount" in sample
            assert sample["postCount"] > 0
        counts = [item["postCount"] for item in data]
        assert counts == sorted(counts, reverse=True)
