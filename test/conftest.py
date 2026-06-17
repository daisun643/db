import pytest

from api.auth import AuthAPI
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
