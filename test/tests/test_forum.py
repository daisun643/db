import pytest
from uuid import uuid4

from api.forum import ForumAPI
from config import TEST_USERS

# 1x1 合法 PNG，用于版块头像上传测试
TINY_PNG = (
    b"\x89PNG\r\n\x1a\n"
    b"\x00\x00\x00\rIHDR"
    b"\x00\x00\x00\x01\x00\x00\x00\x01"
    b"\x08\x02\x00\x00\x00\x90wS\xde"
    b"\x00\x00\x00\x0cIDATx\x9cc\xf8\xff\xff?\x00\x05\xfe\x02\xfeA\xde\xfc\x83"
    b"\x00\x00\x00\x00IEND\xaeB`\x82"
)


CREATOR_FORUM_NAME = "接口测试创建者版块"


def _get_or_create_creator_forum(forum_client):
    """复用固定名称的创建者版块：含帖子的版块无法通过 API 删除，
    复用可避免每次运行套件都残留新版块（创建者权限测试专用）。"""
    for forum in forum_client.get_forums().json():
        if forum["forumName"] == CREATOR_FORUM_NAME:
            return forum["forumID"]
    resp = forum_client.create_forum(CREATOR_FORUM_NAME, "创建者权限测试专用版块")
    assert resp.status_code == 201
    return resp.json()["forumID"]


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
        assert "canAssignManagers" in forum
        assert "avatarUrl" in forum
        assert "memberCount" in forum
        assert "isJoined" in forum
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

            # 创建版块时创建者自动成为版主，且版块一定有版主
            creator = resp.json()["creator"]
            creator_manager = next(
                m for m in resp.json()["managers"] if m["userID"] == creator["userID"]
            )
            assert creator_manager["role"] == "Moderator"
            assert resp.json()["canManage"] is True
            assert resp.json()["canAssignManagers"] is True

            detail = forum_client.get_forum(forum_id)
            assert detail.status_code == 200
            assert detail.json()["forumName"] == forum_name
            assert forum_id in {forum["forumID"] for forum in forum_client.get_forums().json()}
            assert forum_id in {forum["forumID"] for forum in forum_client.get_my_forums().json()}

            # 创建者即默认版主：可以维护自己的版块，但不能删除版块（仅站点管理员可删）
            updated = forum_client.update_forum(forum_id, forum_name, "创建者维护")
            assert updated.status_code == 200
            assert forum_client.delete_forum(forum_id).status_code == 403
            assert forum_client.assign_forum_manager(forum_id, 3).status_code == 200
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

        try:
            forum = admin_forum_client.get_forum(fid).json()
            manager_ids = [m["userID"] for m in forum["managers"]]
            assert 4 in manager_ids
        finally:
            # 种子版块上的指派必须清理，否则污染后续普通用户权限用例
            admin_forum_client.remove_forum_manager(fid, 4)

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

    def test_manager_can_assign_and_remove_manager(
            self, forum_client, admin_forum_client):
        forum_name = f"版主互管-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "版主可互相管理")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            assert admin_forum_client.assign_forum_manager(forum_id, 4).status_code == 200

            assign_resp = forum_client.assign_forum_manager(forum_id, 3)
            assert assign_resp.status_code == 200
            managers = admin_forum_client.get_forum(forum_id).json()["managers"]
            assert 3 in [m["userID"] for m in managers]

            remove_resp = forum_client.remove_forum_manager(forum_id, 3)
            assert remove_resp.status_code == 200
            managers = admin_forum_client.get_forum(forum_id).json()["managers"]
            assert 3 not in [m["userID"] for m in managers]
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_manager_cannot_manage_other_forum(
            self, forum_client, admin_forum_client):
        owned = admin_forum_client.create_forum(
            f"自管版块-{uuid4().hex[:8]}", "当前用户管理")
        foreign = admin_forum_client.create_forum(
            f"他人版块-{uuid4().hex[:8]}", "当前用户不管理")
        assert owned.status_code == 201
        assert foreign.status_code == 201
        owned_id = owned.json()["forumID"]
        foreign_id = foreign.json()["forumID"]

        try:
            assert admin_forum_client.assign_forum_manager(owned_id, 4).status_code == 200
            assert forum_client.assign_forum_manager(foreign_id, 3).status_code == 403
            assert forum_client.remove_forum_manager(foreign_id, 1).status_code == 403
        finally:
            admin_forum_client.delete_forum(owned_id)
            admin_forum_client.delete_forum(foreign_id)

    def test_manager_can_remove_self_and_loses_permission(
            self, forum_client, admin_forum_client):
        forum_name = f"版主自退-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "版主移除自己")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            assert admin_forum_client.assign_forum_manager(forum_id, 4).status_code == 200
            assert forum_client.remove_forum_manager(forum_id, 4).status_code == 200

            managers = admin_forum_client.get_forum(forum_id).json()["managers"]
            assert 4 not in [m["userID"] for m in managers]
            assert forum_client.assign_forum_manager(forum_id, 3).status_code == 403
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_removed_manager_forums_leave_my_forums(
            self, forum_client, admin_forum_client):
        """站点管理员被移除后，“我管理的版块”不应再显示该版块。"""
        create_resp = forum_client.create_forum(
            f"移除验证-{uuid4().hex[:8]}", "站点管理员移除验证")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            assert forum_client.assign_forum_manager(forum_id, 1, "Admin").status_code == 200
            mine = {f["forumID"] for f in admin_forum_client.get_my_forums().json()}
            assert forum_id in mine

            assert forum_client.remove_forum_manager(forum_id, 1).status_code == 200
            mine = {f["forumID"] for f in admin_forum_client.get_my_forums().json()}
            assert forum_id not in mine
        finally:
            forum_client.delete_forum(forum_id)

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

    def test_creator_can_view_own_inactive_forum(
            self, forum_client, moderator_forum_client, admin_forum_client):
        forum_name = f"创建者停用-{uuid4().hex[:8]}"
        create_resp = forum_client.create_forum(forum_name, "创建者可见自己停用的版块")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            update_resp = forum_client.update_forum(
                forum_id, forum_name, "创建者可见自己停用的版块", "Inactive"
            )
            assert update_resp.status_code == 200
            assert update_resp.json()["status"] == "Inactive"

            # 创建者仍可见，其他非站点管理员用户不可见
            assert forum_client.get_forum(forum_id).status_code == 200
            assert forum_id in {
                forum["forumID"] for forum in forum_client.get_forums().json()
            }
            assert moderator_forum_client.get_forum(forum_id).status_code == 404
            assert forum_id not in {
                forum["forumID"] for forum in moderator_forum_client.get_forums().json()
            }
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

    def test_creator_cannot_be_removed_as_manager(
            self, forum_client, admin_forum_client):
        forum_name = f"创建者保护-{uuid4().hex[:8]}"
        create_resp = forum_client.create_forum(forum_name, "创建者是默认版主")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            # 站点管理员也不能移除创建者，保证版块始终有版主
            resp = admin_forum_client.remove_forum_manager(forum_id, 4)
            assert resp.status_code == 400
            assert "创建者" in resp.json()["message"]

            forum = admin_forum_client.get_forum(forum_id).json()
            assert 4 in [m["userID"] for m in forum["managers"]]
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_moderator_can_assign_admin_role(
            self, forum_client, admin_forum_client):
        forum_name = f"版主指派管理员-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "版主可增加管理员")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            # 用户4被指派为版主（缺省角色），版主可再指派管理员角色
            assert admin_forum_client.assign_forum_manager(forum_id, 4).status_code == 200

            assign_resp = forum_client.assign_forum_manager(forum_id, 3, role="Admin")
            assert assign_resp.status_code == 200
            assert assign_resp.json()["message"] == "管理员已指派"
            managers = admin_forum_client.get_forum(forum_id).json()["managers"]
            admin_entry = next(m for m in managers if m["userID"] == 3)
            assert admin_entry["role"] == "Admin"
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_admin_role_manager_cannot_assign_managers(
            self, forum_client, admin_forum_client):
        forum_name = f"管理员越权-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "管理员只能管理帖子")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            assert admin_forum_client.assign_forum_manager(
                forum_id, 4, role="Admin").status_code == 200

            # 管理员角色可以维护版块设置，但不能指派/移除管理人员
            assert forum_client.update_forum(
                forum_id, forum_name, "管理员维护").status_code == 200
            assert forum_client.assign_forum_manager(forum_id, 3).status_code == 403
            assert forum_client.assign_forum_manager(
                forum_id, 3, role="Admin").status_code == 403
            assert forum_client.remove_forum_manager(forum_id, 4).status_code == 403
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_assign_manager_rejects_invalid_role(self, admin_forum_client):
        forum_name = f"角色校验-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "校验角色")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        try:
            resp = admin_forum_client.assign_forum_manager(forum_id, 4, role="Owner")
            assert resp.status_code == 400
            assert resp.json()["message"] == "管理人员角色不合法"
        finally:
            admin_forum_client.delete_forum(forum_id)


class TestUserSearch:

    @pytest.fixture
    def moderator_with_forum(self, moderator_forum_client, admin_forum_client):
        """站点 Moderator（3@）默认无任何版块管理角色，
        指派为临时版块版主后具备版块管理人员身份，可使用用户搜索。"""
        create_resp = admin_forum_client.create_forum(
            f"搜索权限-{uuid4().hex[:8]}", "版块管理人员搜索验证")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]
        try:
            assert admin_forum_client.assign_forum_manager(forum_id, 3).status_code == 200
            yield moderator_forum_client
        finally:
            admin_forum_client.delete_forum(forum_id)

    @staticmethod
    def _fresh_plain_client():
        """注册全新普通用户：测试中 user4 会创建版块成为版主，
        而用户搜索权限按“是否管理任意版块”全局判断，必须用无版块的新用户验证拒绝。"""
        client = ForumAPI()
        email = f"search_plain_{uuid4().hex[:10]}@tongji.edu.cn"
        code = client.post("/api/auth/send-code", json={"email": email}).json().get("debugCode")
        assert code, "当前环境未返回开发调试验证码"
        register_resp = client.post("/api/auth/register", json={
            "email": email,
            "username": f"search_plain_{uuid4().hex[:8]}",
            "password": "Password123",
            "code": code,
        })
        assert register_resp.status_code == 200
        assert client.login(email, "Password123").status_code == 200
        return client

    def test_forum_manager_can_search_users(self, moderator_with_forum):
        resp = moderator_with_forum.search_users(TEST_USERS["user"]["email"])
        assert resp.status_code == 200
        assert 4 in [u["userID"] for u in resp.json()]

    def test_admin_can_search_users(self, admin_forum_client):
        resp = admin_forum_client.search_users(TEST_USERS["user"]["username"])
        assert resp.status_code == 200
        assert any(
            u["username"] == TEST_USERS["user"]["username"] for u in resp.json()
        )

    def test_plain_user_cannot_search_users(self):
        client = self._fresh_plain_client()
        try:
            assert client.search_users("tongji").status_code == 403
        finally:
            client.close()

    def test_unauthenticated_cannot_search_users(self, forum_client):
        forum_client.post("/api/auth/logout")
        assert forum_client.search_users("tongji").status_code == 401

    def test_search_empty_keyword_returns_empty(self, moderator_with_forum):
        resp = moderator_with_forum.search_users("   ")
        assert resp.status_code == 200
        assert resp.json() == []

    def test_search_no_match_returns_empty(self, moderator_with_forum):
        resp = moderator_with_forum.search_users(f"不存在用户-{uuid4().hex}")
        assert resp.status_code == 200
        assert resp.json() == []


class TestForumFollow:

    @staticmethod
    def _create_forum(admin_client, description="关注接口测试"):
        resp = admin_client.create_forum(f"关注测试-{uuid4().hex[:8]}", description)
        assert resp.status_code == 201
        return resp.json()["forumID"]

    def test_forum_summary_includes_follow_fields(self, forum_client):
        forum = forum_client.get_forums().json()[0]
        assert "memberCount" in forum
        assert "isJoined" in forum

    def test_user_can_follow_forum(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_client.leave_forum(forum_id)

            resp = forum_client.join_forum(forum_id)
            assert resp.status_code == 200
            assert resp.json()["joined"] is True
            assert resp.json()["message"] == "关注版块成功"

            forum = forum_client.get_forum(forum_id).json()
            assert forum["isJoined"] is True
            assert forum["memberCount"] == 1
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_follow_is_idempotent(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_client.leave_forum(forum_id)

            first = forum_client.join_forum(forum_id)
            second = forum_client.join_forum(forum_id)
            assert first.status_code == 200
            assert second.status_code == 200
            assert second.json()["message"] == "你已关注该版块"
            assert second.json()["joined"] is True

            assert forum_client.get_forum(forum_id).json()["memberCount"] == 1
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_user_can_unfollow_forum(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_client.leave_forum(forum_id)
            assert forum_client.join_forum(forum_id).status_code == 200

            resp = forum_client.leave_forum(forum_id)
            assert resp.status_code == 200
            assert resp.json()["joined"] is False
            assert resp.json()["message"] == "已取消关注"

            forum = forum_client.get_forum(forum_id).json()
            assert forum["isJoined"] is False
            assert forum["memberCount"] == 0
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_unfollow_without_following_returns_ok(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_client.leave_forum(forum_id)
            resp = forum_client.leave_forum(forum_id)
            assert resp.status_code == 200
            assert resp.json()["message"] == "你尚未关注该版块"
            assert resp.json()["joined"] is False
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_follow_state_is_per_user(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_client.leave_forum(forum_id)
            admin_forum_client.leave_forum(forum_id)

            assert forum_client.join_forum(forum_id).status_code == 200

            assert forum_client.get_forum(forum_id).json()["isJoined"] is True
            assert admin_forum_client.get_forum(forum_id).json()["isJoined"] is False
            assert admin_forum_client.get_forum(forum_id).json()["memberCount"] == 1
        finally:
            admin_forum_client.delete_forum(forum_id)

    @pytest.mark.parametrize("action", ["join", "leave"])
    def test_unauthenticated_cannot_follow_or_unfollow(self, forum_client, action):
        forum_id = forum_client.get_forums().json()[0]["forumID"]
        forum_client.post("/api/auth/logout")
        if action == "join":
            resp = forum_client.join_forum(forum_id)
        else:
            resp = forum_client.leave_forum(forum_id)
        assert resp.status_code == 401

    def test_follow_nonexistent_forum_returns_404(self, forum_client):
        assert forum_client.join_forum(99999).status_code == 404

    def test_inactive_forum_cannot_be_followed_by_regular_user(
            self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_name = admin_forum_client.get_forum(forum_id).json()["forumName"]
            update_resp = admin_forum_client.update_forum(
                forum_id, forum_name, "关注接口测试", "Inactive"
            )
            assert update_resp.status_code == 200

            resp = forum_client.join_forum(forum_id)
            assert resp.status_code == 400
            assert resp.json()["message"] == "该版块已停用，无法关注"
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_admin_can_follow_inactive_forum(self, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_name = admin_forum_client.get_forum(forum_id).json()["forumName"]
            assert admin_forum_client.update_forum(
                forum_id, forum_name, "关注接口测试", "Inactive"
            ).status_code == 200

            admin_forum_client.leave_forum(forum_id)
            resp = admin_forum_client.join_forum(forum_id)
            assert resp.status_code == 200
            assert resp.json()["joined"] is True
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_assigned_manager_can_follow_inactive_forum(
            self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_name = admin_forum_client.get_forum(forum_id).json()["forumName"]
            assert admin_forum_client.update_forum(
                forum_id, forum_name, "关注接口测试", "Inactive"
            ).status_code == 200
            assert admin_forum_client.assign_forum_manager(forum_id, 4).status_code == 200

            forum_client.leave_forum(forum_id)
            resp = forum_client.join_forum(forum_id)
            assert resp.status_code == 200
            assert resp.json()["joined"] is True
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
        for field in ["postID", "title", "contentPreview", "likeCount",
                       "viewCount", "commentCount", "status", "createTime",
                       "userID", "username", "avatarUrl", "forumID", "forumName",
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

    def test_sort_by_latest(self, forum_client):
        resp = forum_client.get_posts(sort="latest")
        assert resp.status_code == 200
        posts = resp.json()
        if len(posts) >= 2:
            assert posts[0]["createTime"] >= posts[1]["createTime"]

    def test_get_posts_returns_total_count(self, forum_client):
        resp = forum_client.get_posts(page=1, pageSize=1)
        assert resp.status_code == 200
        assert "x-total-count" in {key.lower() for key in resp.headers}
        assert int(resp.headers["X-Total-Count"]) >= len(resp.json())
        assert len(resp.json()) <= 1

    @pytest.mark.parametrize("params", [
        {"sort": "unknown"},
        # 热度排序已移除，不再是合法排序参数
        {"sort": "hot"},
        {"from": "2026-07-29T00:00:00", "to": "2026-07-28T00:00:00"},
    ])
    def test_invalid_post_filters_are_rejected(self, forum_client, params):
        resp = forum_client.get_posts(**params)
        assert resp.status_code == 400

    def test_regular_user_cannot_filter_moderation_status(
            self, forum_client, admin_forum_client):
        # 待审核/已封禁等审核状态仅管理人员可按版块筛选，普通用户应被拒绝（即使指定版块）
        assert forum_client.get_posts(status="PendingReview").status_code == 403
        fid = admin_forum_client.get_forums().json()[0]["forumID"]
        assert forum_client.get_posts(
            forumId=fid, status="PendingReview").status_code == 403

    def test_forum_creator_can_filter_pending_posts_in_own_forum(self, forum_client):
        forum_id = _get_or_create_creator_forum(forum_client)

        post_resp = forum_client.create_post(
            forum_id, f"待审核帖子-{uuid4().hex[:8]}", "这里包含敏感词内容")
        assert post_resp.status_code == 201
        assert post_resp.json()["status"] == "PendingReview"
        post_id = post_resp.json()["postID"]

        try:
            resp = forum_client.get_posts(forumId=forum_id, status="PendingReview")
            assert resp.status_code == 200
            assert post_id in [p["postID"] for p in resp.json()]
        finally:
            forum_client.delete_post(post_id)

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

    def test_create_post_missing_title_rejected(self, forum_client):
        fid = self._get_first_forum_id(forum_client)
        resp = forum_client.post("/api/posts", json={
            "forumID": fid, "title": "", "content": "内容",
            "imageUrls": [],
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

    def test_forum_creator_can_pin_others_post_in_own_forum(
            self, forum_client, admin_forum_client):
        forum_id = _get_or_create_creator_forum(forum_client)

        post_resp = admin_forum_client.create_post(
            forum_id, f"他人帖子-{uuid4().hex[:8]}", "管理员发布的帖子")
        assert post_resp.status_code == 201
        post_id = post_resp.json()["postID"]

        try:
            resp = forum_client.change_post_status(post_id, "pin")
            assert resp.status_code == 200
            assert resp.json()["status"] == "Pinned"
        finally:
            forum_client.delete_post(post_id)

    def test_forum_creator_can_delete_others_post_in_own_forum(
            self, forum_client, admin_forum_client):
        forum_id = _get_or_create_creator_forum(forum_client)

        post_resp = admin_forum_client.create_post(
            forum_id, f"待删除他人帖子-{uuid4().hex[:8]}", "内容")
        assert post_resp.status_code == 201
        post_id = post_resp.json()["postID"]

        resp = forum_client.delete_post(post_id)
        assert resp.status_code == 200
        assert forum_client.get_post(post_id).status_code == 404

    def test_regular_user_cannot_moderate_foreign_forum_posts(
            self, forum_client, admin_forum_client):
        forum_name = f"他人版块-{uuid4().hex[:8]}"
        create_resp = admin_forum_client.create_forum(forum_name, "普通用户无权管理")
        assert create_resp.status_code == 201
        forum_id = create_resp.json()["forumID"]

        post_resp = admin_forum_client.create_post(forum_id, "受保护帖子", "内容")
        assert post_resp.status_code == 201
        post_id = post_resp.json()["postID"]

        try:
            assert forum_client.change_post_status(post_id, "pin").status_code == 403
            assert forum_client.delete_post(post_id).status_code == 403
        finally:
            admin_forum_client.delete_post(post_id)
            admin_forum_client.delete_forum(forum_id)


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

    @pytest.fixture(autouse=True)
    def _clean_folders(self, forum_client, admin_forum_client):
        # 收藏夹使用固定名称，唯一约束下需清理残留（含上次失败遗留），保证可重复运行
        for client in (forum_client, admin_forum_client):
            for folder in client.get_favorite_folders().json():
                client.delete_favorite_folder(folder["folderID"])
        yield

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


class TestForumAvatar:

    @staticmethod
    def _create_forum(client, description="版块头像测试"):
        resp = client.create_forum(f"头像测试-{uuid4().hex[:8]}", description)
        assert resp.status_code == 201
        return resp.json()["forumID"]

    def test_creator_can_upload_forum_avatar(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(forum_client)
        try:
            resp = forum_client.upload_forum_avatar(forum_id, "forum-avatar.png", TINY_PNG)
            assert resp.status_code == 200
            avatar_url = resp.json()["avatarUrl"]
            assert avatar_url

            # 头像应在版块详情与列表中可见
            assert forum_client.get_forum(forum_id).json()["avatarUrl"] == avatar_url
            listed = next(
                f for f in forum_client.get_forums().json() if f["forumID"] == forum_id
            )
            assert listed["avatarUrl"] == avatar_url
        finally:
            forum_client.delete_forum_avatar(forum_id)
            admin_forum_client.delete_forum(forum_id)

    def test_creator_can_replace_forum_avatar(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(forum_client)
        try:
            first = forum_client.upload_forum_avatar(forum_id, "first.png", TINY_PNG)
            assert first.status_code == 200

            # 更换头像：二次上传应成功并替换旧头像（回归：曾因外键冲突返回 500）
            second = forum_client.upload_forum_avatar(forum_id, "second.png", TINY_PNG)
            assert second.status_code == 200
            new_url = second.json()["avatarUrl"]
            assert new_url != first.json()["avatarUrl"]
            assert forum_client.get_forum(forum_id).json()["avatarUrl"] == new_url
        finally:
            forum_client.delete_forum_avatar(forum_id)
            admin_forum_client.delete_forum(forum_id)

    def test_avatar_survives_forum_update(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(forum_client)
        try:
            upload = forum_client.upload_forum_avatar(forum_id, "forum-avatar.png", TINY_PNG)
            assert upload.status_code == 200
            avatar_url = upload.json()["avatarUrl"]

            # 保存设置后头像不应丢失（回归：PUT 返回曾丢失 avatarUrl 导致前端渲染错误）
            update = forum_client.update_forum(
                forum_id, f"头像测试-{uuid4().hex[:8]}", "版块头像测试", "Active")
            assert update.status_code == 200
            assert update.json()["avatarUrl"] == avatar_url
            assert forum_client.get_forum(forum_id).json()["avatarUrl"] == avatar_url
        finally:
            forum_client.delete_forum_avatar(forum_id)
            admin_forum_client.delete_forum(forum_id)

    def test_creator_can_delete_forum_avatar(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(forum_client)
        try:
            assert forum_client.upload_forum_avatar(
                forum_id, "forum-avatar.png", TINY_PNG).status_code == 200

            resp = forum_client.delete_forum_avatar(forum_id)
            assert resp.status_code == 200
            assert resp.json()["avatarUrl"] == ""
            assert forum_client.get_forum(forum_id).json()["avatarUrl"] == ""
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_non_manager_cannot_upload_forum_avatar(
            self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            resp = forum_client.upload_forum_avatar(forum_id, "avatar.png", TINY_PNG)
            assert resp.status_code == 403
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_unauthenticated_cannot_upload_forum_avatar(
            self, forum_client, admin_forum_client):
        forum_id = self._create_forum(admin_forum_client)
        try:
            forum_client.post("/api/auth/logout")
            assert forum_client.upload_forum_avatar(
                forum_id, "avatar.png", TINY_PNG).status_code == 401
            assert forum_client.delete_forum_avatar(forum_id).status_code == 401
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_upload_avatar_missing_file_rejected(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(forum_client)
        try:
            resp = forum_client.post(f"/api/forums/{forum_id}/avatar")
            assert resp.status_code == 400
            assert resp.json()["message"] == "请选择头像文件"
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_upload_avatar_rejects_non_image(self, forum_client, admin_forum_client):
        forum_id = self._create_forum(forum_client)
        try:
            resp = forum_client.upload_forum_avatar(
                forum_id, "avatar.txt", b"not image", "text/plain")
            assert resp.status_code == 400
            assert resp.json()["message"] == "仅支持 JPG、PNG、GIF、WebP 图片"
        finally:
            admin_forum_client.delete_forum(forum_id)

    def test_upload_avatar_nonexistent_forum_returns_404(self, forum_client):
        resp = forum_client.upload_forum_avatar(99999, "avatar.png", TINY_PNG)
        assert resp.status_code == 404


class TestMyComments:

    def _get_seed_post_id(self, client):
        return client.get_posts().json()[0]["postID"]

    def test_my_comments_returns_own_comments(self, forum_client):
        pid = self._get_seed_post_id(forum_client)
        content = f"我的评论-{uuid4().hex[:8]}"
        forum_client.create_comment(pid, content)

        resp = forum_client.get_my_comments()
        assert resp.status_code == 200
        mine = next(c for c in resp.json() if c["content"] == content)
        assert mine["postID"] == pid
        assert mine["postTitle"]
        assert mine["forumName"]
        assert "createTime" in mine

    def test_my_comments_excludes_others_comments(
            self, forum_client, admin_forum_client):
        pid = self._get_seed_post_id(forum_client)
        others_content = f"管理员评论-{uuid4().hex[:8]}"
        admin_forum_client.create_comment(pid, others_content)

        contents = [c["content"] for c in forum_client.get_my_comments().json()]
        assert others_content not in contents

    def test_deleted_comment_is_hidden_from_my_comments(self, forum_client):
        pid = self._get_seed_post_id(forum_client)
        content = f"待删除评论-{uuid4().hex[:8]}"
        comment = forum_client.create_comment(pid, content).json()
        assert forum_client.delete_comment(comment["commentID"]).status_code == 200

        contents = [c["content"] for c in forum_client.get_my_comments().json()]
        assert content not in contents

    def test_my_comments_requires_login(self, forum_client):
        forum_client.post("/api/auth/logout")
        assert forum_client.get_my_comments().status_code == 401
