import os

BASE_URL = os.environ.get("BASE_URL", "http://localhost:8080")
TIMEOUT = int(os.environ.get("TEST_TIMEOUT", "10"))

TEST_USERS = {
    "admin": {
        "email": "1@tongji.edu.cn",
        "password": "Password1",
        "username": "Admin User",
    },
    "manager": {
        "email": "2@tongji.edu.cn",
        "password": "Password2",
        "username": "Manager User",
    },
    "moderator": {
        "email": "3@tongji.edu.cn",
        "password": "Password3",
        "username": "Moderator User",
    },
    "user": {
        "email": "4@tongji.edu.cn",
        "password": "Password4",
        "username": "Normal User",
    },
}
