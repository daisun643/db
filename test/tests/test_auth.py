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


def _password_reset_code_or_skip(client, email: str, debug_expires_in_minutes: int | None = None) -> str:
    resp = client.forgot_password(email, debug_expires_in_minutes)
    data = assert_success(resp)
    code = data.get("debugCode")
    if not code:
        pytest.skip("当前环境未返回开发调试验证码，跳过密码重置闭环测试")
    return code


def _register_unique_user(client, prefix: str = "stage2", password: str = "Password123") -> dict:
    email = _unique_email(prefix)
    username = f"{prefix}_{uuid.uuid4().hex[:8]}"
    code = _send_code_or_skip(client, email)
    data = assert_success(client.register(email, username, password, code))
    return {
        "email": email,
        "username": username,
        "password": password,
        "user": data["user"],
    }


def _assert_auth_response_failure(response, message: str | None = None) -> dict:
    assert response.status_code == 200, f"Expected 200, got {response.status_code}: {response.text}"
    data = response.json()
    assert data["success"] is False, f"Expected success=false, got: {data}"
    if message is not None:
        assert data["message"] == message
    return data


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


class TestStage2Profile:

    def test_profile_requires_login(self, client):
        resp = client.get("/api/user/profile")
        assert resp.status_code == 401

    def test_logged_in_user_can_view_own_profile(self, user_client):
        resp = user_client.get("/api/user/profile")

        assert resp.status_code == 200
        data = resp.json()
        assert data["email"] == "4@tongji.edu.cn"
        assert "roles" in data
        assert "permissions" in data

    def test_user_can_update_allowed_profile_fields(self, client):
        created = _register_unique_user(client, "profile")
        new_username = f"profile_{uuid.uuid4().hex[:8]}"

        resp = client.put("/api/user/profile", json={
            "username": new_username,
            "nickname": "阶段二昵称",
            "contact": "wechat: stage2",
            "bio": "阶段二个人简介",
        })

        assert resp.status_code == 200
        data = resp.json()
        assert data["username"] == new_username
        assert data["nickname"] == "阶段二昵称"
        assert data["contact"] == "wechat: stage2"
        assert data["bio"] == "阶段二个人简介"

        profile = client.get("/api/user/profile").json()
        assert profile["userId"] == created["user"]["userId"]
        assert profile["email"] == created["email"]
        assert profile["nickname"] == "阶段二昵称"

    def test_profile_update_ignores_user_id_and_sensitive_fields(self, client):
        created = _register_unique_user(client, "guard")
        before = client.get("/api/user/profile").json()
        new_username = f"guard_{uuid.uuid4().hex[:8]}"

        resp = client.put("/api/user/profile", json={
            "userId": 1,
            "username": new_username,
            "credit": 9999,
            "status": "Disabled",
            "passwordHash": "not-a-real-hash",
            "avatarUrl": "https://example.com/avatar.png",
            "roles": ["Admin"],
            "permissions": ["dashboard.view"],
        })

        assert resp.status_code == 200
        after = client.get("/api/user/profile").json()
        assert after["userId"] == created["user"]["userId"]
        assert after["userId"] != 1
        assert after["username"] == new_username
        assert after["email"] == before["email"]
        assert after["credit"] == before["credit"]
        assert after["status"] == before["status"]
        assert after.get("avatarUrl") in (None, "", before.get("avatarUrl"))
        assert [role["roleName"] for role in after["roles"]] == [role["roleName"] for role in before["roles"]]

        client.logout()
        assert_success(client.login(created["email"], created["password"]))

    def test_user_can_upload_local_avatar(self, client):
        _register_unique_user(client, "avatar")
        png = (
            b"\x89PNG\r\n\x1a\n"
            b"\x00\x00\x00\rIHDR"
            b"\x00\x00\x00\x01\x00\x00\x00\x01"
            b"\x08\x02\x00\x00\x00\x90wS\xde"
            b"\x00\x00\x00\x0cIDATx\x9cc\xf8\xff\xff?\x00\x05\xfe\x02\xfeA\xde\xfc\x83"
            b"\x00\x00\x00\x00IEND\xaeB`\x82"
        )

        resp = client.upload_avatar("avatar.png", png, "image/png")

        assert resp.status_code == 200
        data = resp.json()
        assert data["avatarUrl"].startswith("/uploads/avatars/")
        assert data["avatarUrl"].endswith(".png")

        profile = client.get("/api/user/profile").json()
        assert profile["avatarUrl"] == data["avatarUrl"]

    def test_avatar_upload_rejects_non_image_file(self, client):
        _register_unique_user(client, "avatar_bad")

        resp = client.upload_avatar("avatar.txt", b"not image", "text/plain")

        assert resp.status_code == 400
        assert resp.json()["message"] == "仅支持 JPG、PNG、GIF、WebP 图片"


class TestStage2ChangePassword:

    def test_change_password_rejects_wrong_current_password(self, client):
        _register_unique_user(client, "pwd_wrong")

        resp = client.post("/api/user/password", json={
            "currentPassword": "WrongPassword1",
            "newPassword": "NewPassword123",
        })

        assert resp.status_code == 400
        assert resp.json()["message"] == "当前密码错误"

    def test_change_password_rejects_weak_new_password(self, client):
        created = _register_unique_user(client, "pwd_weak")

        resp = client.post("/api/user/password", json={
            "currentPassword": created["password"],
            "newPassword": "password",
        })

        assert resp.status_code == 400
        assert resp.json()["message"] == "新密码必须至少8位，包含大小写字母和数字"

    def test_change_password_success_and_old_password_fails(self, client):
        created = _register_unique_user(client, "pwd_ok")
        new_password = "NewPassword123"

        resp = client.post("/api/user/password", json={
            "currentPassword": created["password"],
            "newPassword": new_password,
        })
        assert resp.status_code == 200
        assert resp.json()["message"] == "密码已修改"

        assert_success(client.logout())

        old_login = client.login(created["email"], created["password"])
        assert old_login.status_code == 400
        assert old_login.json()["message"] == "邮箱或密码错误"

        assert_success(client.login(created["email"], new_password))


class TestStage3PasswordReset:

    def test_forgot_password_accepts_existing_email(self, client):
        created = _register_unique_user(client, "reset_apply")

        resp = client.forgot_password(created["email"])
        data = assert_success(resp)

        assert data["message"] == "如果该邮箱已注册，重置验证码将发送到您的邮箱"
        assert "debugCode" in data

    def test_forgot_password_does_not_reveal_missing_email(self, client):
        existing = _register_unique_user(client, "reset_no_leak")
        missing_email = _unique_email("missing")

        existing_resp = client.forgot_password(existing["email"])
        missing_resp = client.forgot_password(missing_email)

        existing_data = assert_success(existing_resp)
        missing_data = assert_success(missing_resp)
        assert missing_data["message"] == existing_data["message"]

    def test_reset_password_rejects_wrong_code(self, client):
        created = _register_unique_user(client, "reset_wrong")
        _password_reset_code_or_skip(client, created["email"])

        resp = client.reset_password(created["email"], "000000", "NewPassword123")

        _assert_auth_response_failure(resp, "验证码无效或已过期")

    def test_reset_password_rejects_expired_code(self, client):
        created = _register_unique_user(client, "reset_expired")
        code = _password_reset_code_or_skip(client, created["email"], debug_expires_in_minutes=-1)

        resp = client.reset_password(created["email"], code, "NewPassword123")

        _assert_auth_response_failure(resp, "验证码已过期")

    def test_reset_password_rejects_used_code(self, client):
        created = _register_unique_user(client, "reset_used")
        code = _password_reset_code_or_skip(client, created["email"])

        assert_success(client.reset_password(created["email"], code, "NewPassword123"))
        resp = client.reset_password(created["email"], code, "AnotherPassword123")

        _assert_auth_response_failure(resp, "验证码无效或已过期")

    def test_reset_password_success_old_password_fails_new_password_works(self, client):
        old_password = "Password123"
        new_password = "NewPassword123"
        created = _register_unique_user(client, "reset_success", old_password)
        code = _password_reset_code_or_skip(client, created["email"])

        resp = client.reset_password(created["email"], code, new_password)
        data = assert_success(resp)
        assert data["message"] == "密码重置成功"

        assert_success(client.logout())

        old_login = client.login(created["email"], old_password)
        assert old_login.status_code == 400
        assert old_login.json()["message"] == "邮箱或密码错误"

        assert_success(client.login(created["email"], new_password))
