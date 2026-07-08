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

    def upload_avatar(self, filename: str, content: bytes, content_type: str = "image/png") -> requests.Response:
        return self.post("/api/user/avatar", files={
            "file": (filename, content, content_type),
        })

    def forgot_password(self, email: str, debug_expires_in_minutes: int | None = None) -> requests.Response:
        payload = {"email": email}
        if debug_expires_in_minutes is not None:
            payload["debugExpiresInMinutes"] = debug_expires_in_minutes
        return self.post(f"{self.PREFIX}/forgot-password", json=payload)

    def reset_password(self, email: str, code: str, new_password: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/reset-password", json={
            "email": email,
            "code": code,
            "newPassword": new_password,
        })

    def check_route_access(self, path: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/check-route-access", json={"path": path})

    def get_roles(self) -> requests.Response:
        return self.get("/api/rbac/roles")

    def get_role(self, role_id: int) -> requests.Response:
        return self.get(f"/api/rbac/roles/{role_id}")

    def create_role(self, role_name: str, description: str = "") -> requests.Response:
        return self.post("/api/rbac/roles", json={
            "roleName": role_name,
            "description": description,
        })

    def update_role(self, role_id: int, role_name: str, description: str = "") -> requests.Response:
        return self.put(f"/api/rbac/roles/{role_id}", json={
            "roleName": role_name,
            "description": description,
        })

    def delete_role(self, role_id: int) -> requests.Response:
        return self.delete(f"/api/rbac/roles/{role_id}")

    def get_permissions(self) -> requests.Response:
        return self.get("/api/rbac/permissions")

    def create_permission(
        self,
        permission_name: str,
        description: str = "",
        resource: str = "",
        action: str = "",
    ) -> requests.Response:
        return self.post("/api/rbac/permissions", json={
            "permissionName": permission_name,
            "description": description,
            "resource": resource,
            "action": action,
        })

    def assign_permissions_to_role(self, role_id: int, permission_ids: list[int]) -> requests.Response:
        return self.post(f"/api/rbac/roles/{role_id}/permissions", json={
            "permissionIds": permission_ids,
        })

    def get_user_roles(self, user_id: int) -> requests.Response:
        return self.get(f"/api/rbac/users/{user_id}/roles")

    def assign_roles_to_user(self, user_id: int, role_ids: list[int]) -> requests.Response:
        return self.post(f"/api/rbac/users/{user_id}/roles", json={
            "roleIds": role_ids,
        })
