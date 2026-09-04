import requests

from config import BASE_URL, TIMEOUT


class BaseAPIClient:
    def __init__(self, base_url: str | None = None, timeout: int | None = None):
        self.base_url = (base_url or BASE_URL).rstrip("/")
        self.timeout = timeout or TIMEOUT
        self.session = requests.Session()

    def get(self, path: str, **kwargs) -> requests.Response:
        kwargs.setdefault("timeout", self.timeout)
        return self.session.get(f"{self.base_url}{path}", **kwargs)

    def post(self, path: str, **kwargs) -> requests.Response:
        kwargs.setdefault("timeout", self.timeout)
        return self.session.post(f"{self.base_url}{path}", **kwargs)

    def put(self, path: str, **kwargs) -> requests.Response:
        kwargs.setdefault("timeout", self.timeout)
        return self.session.put(f"{self.base_url}{path}", **kwargs)

    def delete(self, path: str, **kwargs) -> requests.Response:
        kwargs.setdefault("timeout", self.timeout)
        return self.session.delete(f"{self.base_url}{path}", **kwargs)

    def share_session_with(self, other: "BaseAPIClient") -> None:
        """复用 other 的已登录会话。

        单会话机制下同一账号重复登录会使旧会话失效（旧 Cookie 返回 401），
        测试中同一账号需要多种 API 客户端时应共享会话，而非再次登录。
        """
        self.session = other.session

    def close(self):
        self.session.close()
