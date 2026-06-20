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
