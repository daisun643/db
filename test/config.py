import os

BASE_URL = os.environ.get("BASE_URL", "http://localhost:8080")
TIMEOUT = int(os.environ.get("TEST_TIMEOUT", "10"))

TEST_USERS = {
    "admin": {
        "email": "1@tongji.edu.cn",
        "password": "Password1",
        "username": "管理员",
    },
    "manager": {
        "email": "2@tongji.edu.cn",
        "password": "Password2",
        "username": "用户1",
    },
    "moderator": {
        "email": "3@tongji.edu.cn",
        "password": "Password3",
        "username": "用户2",
    },
    "user": {
        "email": "4@tongji.edu.cn",
        "password": "Password4",
        "username": "用户3",
    },
}
