import requests

from api.base import BaseAPIClient


class ForumAPI(BaseAPIClient):
    PREFIX = "/api"

    def login(self, email: str, password: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/auth/login", json={
            "email": email,
            "password": password,
        })

    # ---- Forums ----

    def get_forums(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/forums")

    def get_forum(self, forum_id: int) -> requests.Response:
        return self.get(f"{self.PREFIX}/forums/{forum_id}")

    def get_my_forums(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/forums/mine")

    def create_forum(self, forum_name: str, description: str = "") -> requests.Response:
        return self.post(f"{self.PREFIX}/forums", json={
            "forumName": forum_name,
            "description": description,
        })

    def update_forum(self, forum_id: int, forum_name: str, description: str = "", status: str = "Active") -> requests.Response:
        return self.put(f"{self.PREFIX}/forums/{forum_id}", json={
            "forumName": forum_name,
            "description": description,
            "status": status,
        })

    def delete_forum(self, forum_id: int) -> requests.Response:
        return self.delete(f"{self.PREFIX}/forums/{forum_id}")

    def assign_forum_manager(self, forum_id: int, user_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/forums/{forum_id}/managers", json={
            "userID": user_id,
        })

    def remove_forum_manager(self, forum_id: int, user_id: int) -> requests.Response:
        return self.delete(f"{self.PREFIX}/forums/{forum_id}/managers/{user_id}")

    # ---- Posts ----

    def get_posts(self, **params) -> requests.Response:
        return self.get(f"{self.PREFIX}/posts", params=params)

    def get_post(self, post_id: int) -> requests.Response:
        return self.get(f"{self.PREFIX}/posts/{post_id}")

    def create_post(self, forum_id: int, title: str, content: str,
                    tag_names: list[str] | None = None,
                    image_urls: list[str] | None = None) -> requests.Response:
        return self.post(f"{self.PREFIX}/posts", json={
            "forumID": forum_id,
            "title": title,
            "content": content,
            "tagNames": tag_names or [],
            "imageUrls": image_urls or [],
        })

    def update_post(self, post_id: int, title: str, content: str,
                    tag_names: list[str] | None = None,
                    image_urls: list[str] | None = None) -> requests.Response:
        return self.put(f"{self.PREFIX}/posts/{post_id}", json={
            "title": title,
            "content": content,
            "tagNames": tag_names or [],
            "imageUrls": image_urls or [],
        })

    def delete_post(self, post_id: int) -> requests.Response:
        return self.delete(f"{self.PREFIX}/posts/{post_id}")

    def change_post_status(self, post_id: int, action: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/posts/{post_id}/status", json={
            "action": action,
        })

    def like_post(self, post_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/posts/{post_id}/like")

    def unlike_post(self, post_id: int) -> requests.Response:
        return self.delete(f"{self.PREFIX}/posts/{post_id}/like")

    def get_my_posts(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/posts/me")

    # ---- Favorite folders ----

    def get_favorite_folders(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/favorite-folders")

    def create_favorite_folder(self, folder_name: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/favorite-folders", json={
            "folderName": folder_name,
        })

    def update_favorite_folder(self, folder_id: int, folder_name: str) -> requests.Response:
        return self.put(f"{self.PREFIX}/favorite-folders/{folder_id}", json={
            "folderName": folder_name,
        })

    def delete_favorite_folder(self, folder_id: int) -> requests.Response:
        return self.delete(f"{self.PREFIX}/favorite-folders/{folder_id}")

    def get_favorite_folder_posts(self, folder_id: int) -> requests.Response:
        return self.get(f"{self.PREFIX}/favorite-folders/{folder_id}/posts")

    def add_post_to_favorite_folder(self, folder_id: int, post_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/favorite-folders/{folder_id}/posts/{post_id}")

    def remove_post_from_favorite_folder(self, folder_id: int, post_id: int) -> requests.Response:
        return self.delete(f"{self.PREFIX}/favorite-folders/{folder_id}/posts/{post_id}")

    # ---- Comments ----

    def get_comments(self, post_id: int) -> requests.Response:
        return self.get(f"{self.PREFIX}/posts/{post_id}/comments")

    def create_comment(self, post_id: int, content: str, parent_comment_id: int | None = None) -> requests.Response:
        body: dict = {"content": content}
        if parent_comment_id is not None:
            body["parentCommentID"] = parent_comment_id
        return self.post(f"{self.PREFIX}/posts/{post_id}/comments", json=body)

    def delete_comment(self, comment_id: int) -> requests.Response:
        return self.delete(f"/api/comments/{comment_id}")

    # ---- Reports ----

    def create_report(self, target_type: str, target_id: int, reason: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/reports", json={
            "targetType": target_type,
            "targetID": target_id,
            "reason": reason,
        })

    def get_reports(self, status: str | None = None) -> requests.Response:
        params = {}
        if status is not None:
            params["status"] = status
        return self.get(f"{self.PREFIX}/reports", params=params)

    def review_report(self, report_id: int, action: str, result: str = "") -> requests.Response:
        return self.post(f"{self.PREFIX}/reports/{report_id}/review", json={
            "action": action,
            "result": result,
        })

    # ---- Tags ----

    def get_tags(self, keyword: str = "") -> requests.Response:
        return self.get(f"{self.PREFIX}/tags", params={"keyword": keyword})

    def get_tag_stats(self, top: int = 10) -> requests.Response:
        return self.get(f"{self.PREFIX}/tags/stats", params={"top": top})
