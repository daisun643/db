import pytest

from api.social import SocialAPI
from config import TEST_USERS


def _login(role):
    client = SocialAPI()
    user = TEST_USERS[role]
    response = client.login(user["email"], user["password"])
    assert response.status_code == 200, response.text
    logged_in_user = response.json()["user"]
    logged_in_user["userID"] = logged_in_user["userId"]
    return client, logged_in_user


def _remove_pair(client_a, user_a, client_b, user_b):
    """Best-effort cleanup so this module can be run repeatedly."""
    for client, other_user in ((client_a, user_b), (client_b, user_a)):
        response = client.get_friends()
        if response.status_code == 200:
            for friendship in response.json():
                if friendship["userID"] == other_user["userID"]:
                    client.delete_friend(friendship["friendshipID"])

    for receiver, sender in ((client_a, user_b), (client_b, user_a)):
        response = receiver.get_friend_requests()
        if response.status_code == 200:
            for request in response.json():
                if request["userID"] == sender["userID"]:
                    receiver.reject_friend_request(request["friendshipID"])


@pytest.fixture
def social_users():
    client_a, user_a = _login("admin")
    client_b, user_b = _login("manager")
    client_c, user_c = _login("moderator")
    _remove_pair(client_a, user_a, client_b, user_b)

    yield (client_a, user_a), (client_b, user_b), (client_c, user_c)

    _remove_pair(client_a, user_a, client_b, user_b)
    client_a.close()
    client_b.close()
    client_c.close()


def test_social_endpoints_require_login():
    client = SocialAPI()
    try:
        assert client.get_friends().status_code == 401
        assert client.get_sent_friend_requests().status_code == 401
        assert client.get_messages().status_code == 401
        assert client.get_unread_count().status_code == 401
    finally:
        client.close()


def test_friend_request_validation(social_users):
    (client_a, user_a), (_, _), (_, _) = social_users

    assert client_a.create_friend_request().status_code == 400
    assert client_a.create_friend_request(user_id=user_a["userID"]).status_code == 400
    assert client_a.create_friend_request(user_id=999999).status_code == 404


def test_rejected_request_can_be_sent_again(social_users):
    (client_a, _), (client_b, user_b), (_, _) = social_users

    created = client_a.create_friend_request(user_id=user_b["userID"])
    assert created.status_code == 200, created.text
    friendship_id = created.json()["friendshipID"]
    sent_requests = client_a.get_sent_friend_requests()
    assert sent_requests.status_code == 200, sent_requests.text
    assert any(request["friendshipID"] == friendship_id and request["status"] == "Pending" for request in sent_requests.json())

    rejected = client_b.reject_friend_request(friendship_id)
    assert rejected.status_code == 200, rejected.text
    assert client_b.reject_friend_request(friendship_id).status_code == 409
    sent_requests = client_a.get_sent_friend_requests()
    assert any(request["friendshipID"] == friendship_id and request["status"] == "Rejected" for request in sent_requests.json())

    resent = client_a.create_friend_request(user_id=user_b["userID"])
    assert resent.status_code == 200, resent.text
    assert resent.json()["status"] == "Pending"
    sent_requests = client_a.get_sent_friend_requests()
    assert any(request["friendshipID"] == friendship_id and request["status"] == "Pending" for request in sent_requests.json())


def test_friendship_and_private_message_flow(social_users):
    (client_a, user_a), (client_b, user_b), (client_c, _) = social_users

    created = client_a.create_friend_request(user_id=user_b["userID"])
    assert created.status_code == 200, created.text
    friendship_id = created.json()["friendshipID"]
    assert created.json()["status"] == "Pending"

    assert client_a.create_friend_request(user_id=user_b["userID"]).status_code == 400
    assert client_b.create_friend_request(user_id=user_a["userID"]).status_code == 400
    assert client_a.accept_friend_request(friendship_id).status_code == 403

    accepted = client_b.accept_friend_request(friendship_id)
    assert accepted.status_code == 200, accepted.text
    assert client_b.accept_friend_request(friendship_id).status_code == 409

    sent_requests = client_a.get_sent_friend_requests()
    assert any(request["friendshipID"] == friendship_id and request["status"] == "Accepted" for request in sent_requests.json())
    assert any(friend["userID"] == user_b["userID"] for friend in client_a.get_friends().json())
    assert any(friend["userID"] == user_a["userID"] for friend in client_b.get_friends().json())

    assert client_c.send_message(user_a["userID"], "越权消息").status_code == 400
    assert client_a.send_message(user_a["userID"], "发给自己").status_code == 400
    assert client_a.send_message(user_b["userID"], "   ").status_code == 400

    unread_before = client_b.get_unread_count().json()["count"]
    sent = client_a.send_message(user_b["userID"], "第一条测试私信")
    assert sent.status_code == 200, sent.text
    message = sent.json()
    assert message["isRead"] is False
    assert client_b.get_unread_count().json()["count"] == unread_before + 1

    history = client_b.get_messages(user_a["userID"])
    assert history.status_code == 200, history.text
    assert message["messageID"] in [item["messageID"] for item in history.json()]
    send_times = [item["sendTime"] for item in history.json()]
    assert send_times == sorted(send_times)

    outsider_history = client_c.get_messages(user_a["userID"])
    assert message["messageID"] not in [item["messageID"] for item in outsider_history.json()]
    assert client_a.mark_message_read(message["messageID"]).status_code == 403
    assert client_c.mark_message_read(message["messageID"]).status_code == 403

    marked = client_b.mark_conversation_read(user_a["userID"])
    assert marked.status_code == 200, marked.text
    assert marked.json()["count"] >= 1
    assert client_b.get_unread_count().json()["count"] == unread_before

    deleted = client_a.delete_friend(friendship_id)
    assert deleted.status_code == 200, deleted.text
    assert client_a.send_message(user_b["userID"], "删除好友后发送").status_code == 400

    retained_history = client_b.get_messages(user_a["userID"]).json()
    assert message["messageID"] in [item["messageID"] for item in retained_history]
