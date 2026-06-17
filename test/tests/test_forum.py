import pytest


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


class TestForumCRUD:

    def test_admin_can_create_forum(self, admin_forum_client):
        resp = admin_forum_client.create_forum("测试论坛", "这是一个测试论坛")
        assert resp.status_code == 201
        data = resp.json()
        assert data["forumName"] == "测试论坛"
        assert data["description"] == "这是一个测试论坛"
        assert data["status"] == "Active"

    def test_user_cannot_create_forum(self, forum_client):
        resp = forum_client.create_forum("非法论坛", "普通用户不应能创建")
        assert resp.status_code == 403

    def test_admin_can_update_forum(self, admin_forum_client):
        create_resp = admin_forum_client.create_forum("待修改论坛", "原描述")
        fid = create_resp.json()["forumID"]
        resp = admin_forum_client.update_forum(fid, "修改后论坛", "新描述", "Active")
        assert resp.status_code == 200
        assert resp.json()["forumName"] == "修改后论坛"
        assert resp.json()["description"] == "新描述"

    def test_user_cannot_update_forum(self, forum_client, admin_forum_client):
        create_resp = admin_forum_client.create_forum("受保护论坛", "描述")
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


class TestForumManagers:

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

    def test_sort_by_latest(self, forum_client):
        resp = forum_client.get_posts(sort="latest")
        assert resp.status_code == 200
        posts = resp.json()
        if len(posts) >= 2:
            assert posts[0]["createTime"] >= posts[1]["createTime"]

    def test_sort_by_hot(self, forum_client):
        resp = forum_client.get_posts(sort="hot")
        assert resp.status_code == 200

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
