import uuid

import pytest

from utils.assertions import assert_success, assert_failure


def _unique_email(prefix: str = "stage1") -> str:
    return f"{prefix}_{uuid.uuid4().hex[:10]}@tongji.edu.cn"


def _send_code_or_skip(client, email: str) -> str:
    resp = client.send_code(email)
    data = assert_success(resp)
    code = data.get("debugCode")
    if not code:
        pytest.skip("当前环境未返回开发调试验证码，跳过注册闭环测试")
    return code


class TestStage1Registration:

    def test_register_success_with_tongji_email(self, client):
        email = _unique_email()
        username = f"user_{uuid.uuid4().hex[:8]}"
        code = _send_code_or_skip(client, email)

        resp = client.register(email, username, "Password123", code)
        data = assert_success(resp)

        assert data["message"] == "注册成功"
        assert data["user"]["email"] == email
        assert data["user"]["username"] == username
        assert "TongjiForumAuth" in {cookie.name for cookie in client.session.cookies}

    def test_register_duplicate_email_rejected(self, client):
        email = _unique_email("dup_email")
        first_username = f"user_{uuid.uuid4().hex[:8]}"
        code = _send_code_or_skip(client, email)
        assert_success(client.register(email, first_username, "Password123", code))

        resp = client.register(email, f"user_{uuid.uuid4().hex[:8]}", "Password123", "000000")
        assert_failure(resp, message="该邮箱已被注册")

    def test_register_duplicate_username_rejected(self, client):
        username = f"user_{uuid.uuid4().hex[:8]}"
        first_email = _unique_email("dup_name_a")
        first_code = _send_code_or_skip(client, first_email)
        assert_success(client.register(first_email, username, "Password123", first_code))

        second_email = _unique_email("dup_name_b")
        second_code = _send_code_or_skip(client, second_email)
        resp = client.register(second_email, username, "Password123", second_code)
        assert_failure(resp, message="该用户名已被使用")

    def test_register_non_tongji_email_rejected(self, client):
        resp = client.register("student@example.com", "external_user", "Password123", "123456")
        assert_failure(resp, message="仅支持 @tongji.edu.cn 邮箱注册")

    def test_register_weak_password_rejected(self, client):
        email = _unique_email("weak")
        code = _send_code_or_skip(client, email)
        resp = client.register(email, f"user_{uuid.uuid4().hex[:8]}", "password", code)
        assert_failure(resp, message="密码必须包含大小写字母和数字")


class TestStage1CurrentUser:

    def test_auth_me_returns_roles_permissions_and_level_fields(self, admin_client):
        resp = admin_client.me()
        data = assert_success(resp)
        user = data["user"]

        assert user["email"] == "1@tongji.edu.cn"
        assert "roles" in user
        assert "permissions" in user
        assert "Admin" in user["roles"]
        assert "dashboard.view" in user["permissions"]
        assert isinstance(user["userLevel"], int)
        assert isinstance(user["totalCredit"], int)
