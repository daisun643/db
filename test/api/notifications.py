import requests

from api.base import BaseAPIClient


class NotificationAPI(BaseAPIClient):
    PREFIX = "/api"

    def login(self, email: str, password: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/auth/login", json={
            "email": email,
            "password": password,
        })

    def get_notifications(self, **params) -> requests.Response:
        return self.get(f"{self.PREFIX}/notifications", params=params)

    def get_unread_count(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/notifications/unread-count")

    def mark_read(self, notification_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/notifications/{notification_id}/read")

    def mark_all_read(self) -> requests.Response:
        return self.post(f"{self.PREFIX}/notifications/read-all")

    def delete_notification(self, notification_id: int) -> requests.Response:
        return self.delete(f"{self.PREFIX}/notifications/{notification_id}")

    def create_system_notification(self, title: str, content: str, receiver_user_ids: list[int] | None = None) -> requests.Response:
        body: dict = {
            "title": title,
            "content": content,
        }
        if receiver_user_ids is not None:
            body["receiverUserIDs"] = receiver_user_ids
        return self.post(f"{self.PREFIX}/notifications/system", json=body)

    def get_pending_audits(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/audits/posts")

    def approve_audit(self, audit_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/audits/posts/{audit_id}/approve")

    def reject_audit(self, audit_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/audits/posts/{audit_id}/reject")
