import pytest

from api.auth import AuthAPI
from api.forum import ForumAPI
from config import TEST_USERS


@pytest.fixture
def client():
    c = AuthAPI()
    yield c
    c.close()


@pytest.fixture
def admin_client():
    c = AuthAPI()
    c.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
    yield c
    c.close()


@pytest.fixture
def user_client():
    c = AuthAPI()
    c.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
    yield c
    c.close()


@pytest.fixture
def forum_client():
    c = ForumAPI()
    c.login(TEST_USERS["user"]["email"], TEST_USERS["user"]["password"])
    yield c
    c.close()


@pytest.fixture
def admin_forum_client():
    c = ForumAPI()
    c.login(TEST_USERS["admin"]["email"], TEST_USERS["admin"]["password"])
    yield c
    c.close()


@pytest.fixture
def moderator_forum_client():
    c = ForumAPI()
    c.login(TEST_USERS["moderator"]["email"], TEST_USERS["moderator"]["password"])
    yield c
    c.close()


@pytest.fixture
def manager_forum_client():
    c = ForumAPI()
    c.login(TEST_USERS["manager"]["email"], TEST_USERS["manager"]["password"])
    yield c
    c.close()
