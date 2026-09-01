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


def _valid_png() -> bytes:
    """最小合法 PNG，供头像上传测试复用。"""
    return (
        b"\x89PNG\r\n\x1a\n"
        b"\x00\x00\x00\rIHDR"
        b"\x00\x00\x00\x01\x00\x00\x00\x01"
        b"\x08\x02\x00\x00\x00\x90wS\xde"
        b"\x00\x00\x00\x0cIDATx\x9cc\xf8\xff\xff?\x00\x05\xfe\x02\xfeA\xde\xfc\x83"
        b"\x00\x00\x00\x00IEND\xaeB`\x82"
    )


def _assert_auth_response_failure(response, message: str | None = None) -> dict:
    assert response.status_code == 200, f"Expected 200, got {response.status_code}: {response.text}"
    data = response.json()
    assert data["success"] is False, f"Expected success=false, got: {data}"
    if message is not None:
        assert data["message"] == message
    return data


def _entity_id(entity: dict, *names: str) -> int:
    for name in names:
        if name in entity and entity[name] is not None:
            return int(entity[name])
    raise AssertionError(f"Missing id field {names} in {entity}")


def _role_id(role: dict) -> int:
    return _entity_id(role, "roleID", "roleId")


def _permission_id(permission: dict) -> int:
    return _entity_id(permission, "permissionID", "permissionId")


def _role_name(role: dict) -> str:
    return role.get("roleName") or role.get("RoleName") or ""


def _permission_name(permission: dict) -> str:
    return permission.get("permissionName") or permission.get("PermissionName") or ""


def _get_role_by_name(client, role_name: str) -> dict:
    resp = client.get_roles()
    assert resp.status_code == 200, resp.text
    for role in resp.json():
        if _role_name(role).lower() == role_name.lower():
            return role
    raise AssertionError(f"Role not found: {role_name}")


def _get_permission_by_name(client, permission_name: str) -> dict:
    resp = client.get_permissions()
    assert resp.status_code == 200, resp.text
    for permission in resp.json():
        if _permission_name(permission).lower() == permission_name.lower():
            return permission
    raise AssertionError(f"Permission not found: {permission_name}")


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

    def test_register_username_with_whitespace_rejected(self, client):
        resp = client.register(_unique_email("space_name"), "user name 1", "Password123", "000000")
        assert_failure(resp, message="用户名不能包含空格")

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
            "contact": "wechat: stage2",
            "bio": "阶段二个人简介",
        })

        assert resp.status_code == 200
        data = resp.json()
        assert data["username"] == new_username
        assert data["contact"] == "wechat: stage2"
        assert data["bio"] == "阶段二个人简介"
        assert "nickname" not in data

        profile = client.get("/api/user/profile").json()
        assert profile["userId"] == created["user"]["userId"]
        assert profile["email"] == created["email"]
        assert profile["username"] == new_username

    def test_profile_update_username_with_whitespace_rejected(self, client):
        created = _register_unique_user(client, "profile_space")
        before = client.get("/api/user/profile").json()

        resp = client.put("/api/user/profile", json={
            "username": "bad username",
        })

        # DTO 正则校验与控制器内校验均返回 400，响应体可能为 {message} 或 ModelState {errors}
        assert resp.status_code == 400
        data = resp.json()
        if "message" in data:
            assert data["message"] == "用户名不能包含空格"
        else:
            assert "errors" in data

        # 用户名未被修改
        profile = client.get("/api/user/profile").json()
        assert profile["username"] == before["username"] == created["user"]["username"]

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
        png = _valid_png()

        resp = client.upload_avatar("avatar.png", png, "image/png")

        assert resp.status_code == 200
        data = resp.json()
        assert data["avatarUrl"].startswith("/uploads/avatars/")
        assert data["avatarUrl"].endswith(".png")

        profile = client.get("/api/user/profile").json()
        assert profile["avatarUrl"] == data["avatarUrl"]

    def test_user_can_replace_existing_avatar(self, client):
        """回归覆盖：替换已有头像时必须先删除关联再删除媒体记录。"""
        _register_unique_user(client, "avatar_replace")

        first = client.upload_avatar("avatar-first.png", _valid_png(), "image/png")
        assert first.status_code == 200, first.text
        first_url = first.json()["avatarUrl"]

        second = client.upload_avatar("avatar-second.png", _valid_png(), "image/png")

        assert second.status_code == 200, second.text
        second_url = second.json()["avatarUrl"]
        assert second_url.startswith("/uploads/avatars/")
        assert second_url.endswith(".png")
        assert second_url != first_url

        profile = client.get("/api/user/profile").json()
        assert profile["avatarUrl"] == second_url

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


class TestStage4Rbac:

    def test_normal_user_cannot_access_rbac_metadata(self, user_client):
        assert user_client.get_roles().status_code == 403

    def test_manager_cannot_access_rbac_metadata(self, client):
        assert_success(client.login("2@tongji.edu.cn", "Password2"))
        assert client.get_roles().status_code == 403

    def test_admin_can_read_predefined_roles_and_permissions(self, admin_client):
        roles = admin_client.get_roles()
        permissions = admin_client.get_permissions()

        assert roles.status_code == 200
        assert permissions.status_code == 200
        assert {role["roleName"] for role in roles.json()} >= {"Admin", "User"}
        assert any(permission["permissionName"] == "forums.view" for permission in permissions.json())

    @pytest.mark.parametrize("mutation", [
        lambda client: client.create_role("dynamic-role"),
        lambda client: client.update_role(1, "renamed-role"),
        lambda client: client.delete_role(1),
        lambda client: client.create_permission("dynamic.permission"),
        lambda client: client.assign_permissions_to_role(1, []),
        lambda client: client.assign_roles_to_user(4, []),
    ])
    def test_rbac_mutation_endpoints_are_removed(self, admin_client, mutation):
        assert mutation(admin_client).status_code in (404, 405)

    def test_admin_can_read_existing_user_roles(self, admin_client):
        resp = admin_client.get_user_roles(4)
        assert resp.status_code == 200
        assert [role["roleName"] for role in resp.json()] == ["User"]


class TestStage5BackendEntryAccess:

    def test_guest_cannot_check_backend_route_access(self, client):
        resp = client.check_route_access("/system-status")

        assert resp.status_code == 401

    def test_normal_user_cannot_access_backend_entry(self, user_client):
        resp = user_client.check_route_access("/system-status")

        assert resp.status_code == 200
        assert resp.json()["hasAccess"] is False

    def test_admin_can_access_backend_entry(self, admin_client):
        resp = admin_client.check_route_access("/system-status")

        assert resp.status_code == 200
        assert resp.json()["hasAccess"] is True

    def test_dashboard_view_user_can_access_backend_entry(self, client):
        assert_success(client.login("2@tongji.edu.cn", "Password2"))

        resp = client.check_route_access("/system-status?tab=rbac#roles")

        assert resp.status_code == 200
        data = resp.json()
        assert data["path"] == "/system-status"
        assert data["hasAccess"] is True

    @pytest.mark.parametrize("path", ["/", "/forums", "/products", "/messages", "/profile"])
    def test_frontend_menu_paths_are_known_by_backend_route_map(self, user_client, path):
        resp = user_client.check_route_access(path)

        assert resp.status_code == 200
        assert resp.json()["path"] == path
        assert resp.json()["hasAccess"] is True

    def test_unknown_route_is_not_allowed(self, admin_client):
        resp = admin_client.check_route_access("/unknown-admin")

        assert resp.status_code == 200
        assert resp.json()["path"] == "/unknown-admin"
        assert resp.json()["hasAccess"] is False


class TestStage6Credit:

    def test_profile_exposes_credit_level_and_total_credit(self, user_client):
        resp = user_client.get("/api/user/profile")

        assert resp.status_code == 200
        data = resp.json()
        assert isinstance(data["credit"], int)
        assert isinstance(data["userLevel"], int)
        assert isinstance(data["totalCredit"], int)

    def test_admin_can_view_user_credit(self, admin_client):
        resp = admin_client.get_user_credit(4)

        assert resp.status_code == 200
        data = resp.json()
        assert data["userId"] == 4
        assert isinstance(data["credit"], int)
        assert isinstance(data["userLevel"], int)
        assert isinstance(data["totalCredit"], int)

    def test_normal_user_cannot_adjust_credit(self, user_client):
        resp = user_client.adjust_credit(4, 10, "stage6 forbidden")

        assert resp.status_code == 403

    def test_admin_adjust_credit_records_full_audit_fields(self, admin_client, client):
        created = _register_unique_user(client, "stage6_credit")
        user_id = created["user"]["userId"]
        before = admin_client.get_user_credit(user_id).json()["credit"]

        resp = admin_client.adjust_credit(user_id, 15, "stage6 audit")

        assert resp.status_code == 200, resp.text
        data = resp.json()
        assert data["message"] == "信用分调整成功"

        adjustment = data["adjustment"]
        assert adjustment["userId"] == user_id
        assert adjustment["description"] == "stage6 audit"
        assert adjustment["beforeCredit"] == before
        assert adjustment["afterCredit"] == before + 15
        assert adjustment["changePoints"] == 15
        assert adjustment["operatorId"] == 1
        assert adjustment["operatorName"]
        assert adjustment["adjustTime"]

        current_user_records = client.get_credit_adjustments()
        assert current_user_records.status_code == 200
        assert any(item["creditAdjustmentId"] == adjustment["creditAdjustmentId"] for item in current_user_records.json())

        admin_records = admin_client.get_user_credit_adjustments(user_id)
        assert admin_records.status_code == 200
        assert any(item["creditAdjustmentId"] == adjustment["creditAdjustmentId"] for item in admin_records.json())

    def test_credit_adjustment_is_clamped_to_bounds(self, admin_client, client):
        created = _register_unique_user(client, "stage6_clamp")
        user_id = created["user"]["userId"]
        before = admin_client.get_user_credit(user_id).json()["credit"]

        down_resp = admin_client.adjust_credit(user_id, -5000, "stage6 clamp down")

        assert down_resp.status_code == 200, down_resp.text
        down_adjustment = down_resp.json()["adjustment"]
        assert down_adjustment["beforeCredit"] == before
        assert down_adjustment["afterCredit"] == 0
        assert down_adjustment["changePoints"] == -before

        up_resp = admin_client.adjust_credit(user_id, 5000, "stage6 clamp up")

        assert up_resp.status_code == 200, up_resp.text
        up_adjustment = up_resp.json()["adjustment"]
        assert up_adjustment["beforeCredit"] == 0
        assert up_adjustment["afterCredit"] == 1000
        assert up_adjustment["changePoints"] == 1000

    def test_adjust_credit_requires_reason_and_nonzero_change(self, admin_client):
        zero_resp = admin_client.adjust_credit(4, 0, "stage6 zero")
        blank_reason_resp = admin_client.adjust_credit(4, 10, "   ")

        assert zero_resp.status_code == 400
        assert blank_reason_resp.status_code == 400


class TestStage7MemberOneCoverage:

    def test_login_success_and_wrong_password_failure(self, client):
        success_resp = client.login("1@tongji.edu.cn", "Password1")
        success_data = assert_success(success_resp)
        assert success_data["message"] == "登录成功"
        assert success_data["user"]["email"] == "1@tongji.edu.cn"

        assert_success(client.logout())

        wrong_resp = client.login("1@tongji.edu.cn", "WrongPassword1")
        assert wrong_resp.status_code == 400
        assert wrong_resp.json()["message"] == "邮箱或密码错误"

    def test_profile_payload_cannot_modify_another_user(self, client):
        victim = _register_unique_user(client, "stage7_victim")
        assert_success(client.logout())

        attacker = _register_unique_user(client, "stage7_attacker")
        attacker_new_username = f"stage7_attacker_{uuid.uuid4().hex[:8]}"

        resp = client.put("/api/user/profile", json={
            "userId": victim["user"]["userId"],
            "username": attacker_new_username,
        })

        assert resp.status_code == 200
        attacker_profile = client.get("/api/user/profile").json()
        assert attacker_profile["userId"] == attacker["user"]["userId"]
        assert attacker_profile["username"] == attacker_new_username

        assert_success(client.logout())
        assert_success(client.login(victim["email"], victim["password"]))
        victim_profile = client.get("/api/user/profile").json()
        assert victim_profile["userId"] == victim["user"]["userId"]
        assert victim_profile["username"] == victim["username"]

    def test_normal_user_cannot_access_admin_user_interfaces(self, user_client):
        list_resp = user_client.get("/api/users")
        create_resp = user_client.post("/api/users", json={
            "username": f"stage7_admin_api_{uuid.uuid4().hex[:8]}",
            "email": _unique_email("stage7_admin_api"),
            "password": "Password123",
            "roleIds": [],
        })

        assert list_resp.status_code == 403
        assert create_resp.status_code == 403

    def test_admin_created_user_always_receives_default_role(self, admin_client):
        resp = admin_client.post("/api/users", json={
            "username": f"stage7_default_{uuid.uuid4().hex[:8]}",
            "email": _unique_email("stage7_default"),
            "password": "Password123",
            "roleIds": [1],
        })

        assert resp.status_code == 201, resp.text
        assert resp.json()["roles"] == ["User"]
