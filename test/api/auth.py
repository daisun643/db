import requests

from api.base import BaseAPIClient


class AuthAPI(BaseAPIClient):
    PREFIX = "/api/auth"

    def login(self, email: str, password: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/login", json={
            "email": email,
            "password": password,
        })

    def logout(self) -> requests.Response:
        return self.post(f"{self.PREFIX}/logout")

    def me(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/me")

    def send_code(self, email: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/send-code", json={"email": email})

    def register(self, email: str, username: str, password: str, code: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/register", json={
            "email": email,
            "username": username,
            "password": password,
            "code": code,
        })

    def forgot_password(self, email: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/forgot-password", json={"email": email})

    def reset_password(self, email: str, code: str, new_password: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/reset-password", json={
            "email": email,
            "code": code,
            "newPassword": new_password,
        })

    def check_route_access(self, path: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/check-route-access", json={"path": path})
