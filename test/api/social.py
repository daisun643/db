from api.auth import AuthAPI


class SocialAPI(AuthAPI):
    FRIENDS_PREFIX = "/api/friends"
    MESSAGES_PREFIX = "/api/messages"

    def get_friends(self):
        return self.get(self.FRIENDS_PREFIX)

    def get_friend_requests(self):
        return self.get(f"{self.FRIENDS_PREFIX}/requests")

    def get_sent_friend_requests(self):
        return self.get(f"{self.FRIENDS_PREFIX}/sent")

    def create_friend_request(self, *, user_id=None, email=None):
        payload = {}
        if user_id is not None:
            payload["userID"] = user_id
        if email is not None:
            payload["email"] = email
        return self.post(self.FRIENDS_PREFIX, json=payload)

    def accept_friend_request(self, friendship_id):
        return self.post(f"{self.FRIENDS_PREFIX}/{friendship_id}/accept")

    def reject_friend_request(self, friendship_id):
        return self.post(f"{self.FRIENDS_PREFIX}/{friendship_id}/reject")

    def delete_friend(self, friendship_id):
        return self.delete(f"{self.FRIENDS_PREFIX}/{friendship_id}")

    def get_messages(self, user_id=None):
        params = {"userId": user_id} if user_id is not None else None
        return self.get(self.MESSAGES_PREFIX, params=params)

    def send_message(self, receiver_id, content):
        return self.post(self.MESSAGES_PREFIX, json={
            "receiverID": receiver_id,
            "content": content,
        })

    def mark_message_read(self, message_id):
        return self.post(f"{self.MESSAGES_PREFIX}/{message_id}/read")

    def mark_conversation_read(self, user_id):
        return self.post(f"{self.MESSAGES_PREFIX}/read-all", params={"userId": user_id})

    def get_unread_count(self):
        return self.get(f"{self.MESSAGES_PREFIX}/unread-count")
