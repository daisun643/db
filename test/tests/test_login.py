import pytest

from config import TEST_USERS
from utils.assertions import assert_success, assert_failure


class TestLoginSuccess:

    @pytest.mark.parametrize("role", TEST_USERS.keys())
    def test_login_with_preset_user(self, client, role):
        user = TEST_USERS[role]
        resp = client.login(user["email"], user["password"])
        data = assert_success(resp)

        assert data["user"]["email"] == user["email"]
        assert data["user"]["username"] == user["username"]
        assert data["message"] == "登录成功"

    def test_login_sets_auth_cookie(self, client):
        user = TEST_USERS["admin"]
        resp = client.login(user["email"], user["password"])
        assert_success(resp)

        cookies = client.session.cookies
        cookie_names = [c.name for c in cookies]
        assert any("TongjiForumAuth" in name for name in cookie_names), \
            f"Expected auth cookie, got: {cookie_names}"

    def test_login_then_me_returns_current_user(self, client):
        user = TEST_USERS["admin"]
        client.login(user["email"], user["password"])

        resp = client.me()
        data = assert_success(resp)
        assert data["user"]["email"] == user["email"]

    def test_login_returns_user_credit(self, client):
        resp = client.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
        data = assert_success(resp)
        assert "credit" in data["user"]
        assert isinstance(data["user"]["credit"], int)


class TestLoginFailure:

    def test_wrong_password(self, client):
        resp = client.login("1@tongji.edu.cn", "WrongPassword1")
        assert_failure(resp, message="邮箱或密码错误")

    def test_nonexistent_email(self, client):
        resp = client.login("notexist@tongji.edu.cn", "Password1")
        assert_failure(resp, message="邮箱或密码错误")

    def test_missing_password(self, client):
        resp = client.post("/api/auth/login", json={"email": "1@tongji.edu.cn"})
        assert_failure(resp)

    def test_missing_email(self, client):
        resp = client.post("/api/auth/login", json={"password": "Password1"})
        assert_failure(resp)

    def test_empty_body(self, client):
        resp = client.post("/api/auth/login", json={})
        assert_failure(resp)

    def test_invalid_email_format(self, client):
        resp = client.login("not-an-email", "Password1")
        assert_failure(resp)

    def test_wrong_password_does_not_leak_info(self, client):
        resp_wrong = client.login("1@tongji.edu.cn", "WrongPass1")
        resp_noexist = client.login("ghost@tongji.edu.cn", "Password1")
        assert resp_wrong.json()["message"] == resp_noexist.json()["message"]


class TestSingleSession:

    def test_second_login_invalidates_first_login_cookie(self, client):
        user = TEST_USERS["user"]
        other_client = type(client)()
        try:
            assert_success(client.login(user["email"], user["password"]))
            assert_success(client.me())

            assert_success(other_client.login(user["email"], user["password"]))
            assert_success(other_client.me())

            assert client.me().status_code == 401
        finally:
            other_client.close()

    def test_failed_second_login_does_not_invalidate_current_cookie(self, client):
        user = TEST_USERS["user"]
        other_client = type(client)()
        try:
            assert_success(client.login(user["email"], user["password"]))

            failed_login = other_client.login(user["email"], "WrongPassword1")
            assert_failure(failed_login, message="邮箱或密码错误")

            assert_success(client.me())
        finally:
            other_client.close()

    def test_different_users_do_not_invalidate_each_other(self, client):
        user = TEST_USERS["user"]
        admin = TEST_USERS["admin"]
        other_client = type(client)()
        try:
            assert_success(client.login(user["email"], user["password"]))
            assert_success(other_client.login(admin["email"], admin["password"]))

            assert_success(client.me())
            assert_success(other_client.me())
        finally:
            other_client.close()

    def test_logout_invalidates_a_copied_cookie(self, client):
        user = TEST_USERS["user"]
        copied_cookie_client = type(client)()
        try:
            assert_success(client.login(user["email"], user["password"]))
            copied_cookie_client.session.cookies.update(client.session.cookies)
            assert_success(copied_cookie_client.me())

            assert_success(client.logout())

            assert copied_cookie_client.me().status_code == 401
        finally:
            copied_cookie_client.close()

    def test_stale_cookie_cannot_logout_the_new_session(self, client):
        user = TEST_USERS["user"]
        current_client = type(client)()
        try:
            assert_success(client.login(user["email"], user["password"]))
            assert_success(current_client.login(user["email"], user["password"]))

            assert client.logout().status_code == 401
            assert_success(current_client.me())
        finally:
            current_client.close()

    def test_stale_client_can_log_in_again_and_become_current(self, client):
        user = TEST_USERS["user"]
        other_client = type(client)()
        try:
            assert_success(client.login(user["email"], user["password"]))
            assert_success(other_client.login(user["email"], user["password"]))

            assert_success(client.login(user["email"], user["password"]))
            assert_success(client.me())
            assert other_client.me().status_code == 401
        finally:
            other_client.close()


class TestLoginLogout:

    def test_logout_then_me_returns_unauthorized(self, client):
        user = TEST_USERS["admin"]
        client.login(user["email"], user["password"])
        assert_success(client.me())

        resp = client.logout()
        assert_success(resp, )
        assert resp.json()["message"] == "登出成功"

        me_resp = client.me()
        assert me_resp.status_code == 401

    def test_me_without_login_returns_unauthorized(self, client):
        resp = client.me()
        assert resp.status_code == 401


class TestRouteAccess:

    def test_admin_can_access_system_status(self, admin_client):
        resp = admin_client.check_route_access("/system-status")
        assert resp.status_code == 200
        assert resp.json()["hasAccess"] is True

    def test_user_cannot_access_system_status(self, user_client):
        resp = user_client.check_route_access("/system-status")
        assert resp.status_code == 200
        assert resp.json()["hasAccess"] is False

    def test_any_user_can_access_home(self, user_client):
        resp = user_client.check_route_access("/")
        assert resp.status_code == 200
        assert resp.json()["hasAccess"] is True
