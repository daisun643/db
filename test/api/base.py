from http.cookies import SimpleCookie

import requests

from config import BASE_URL, TIMEOUT


class BaseAPIClient:
    def __init__(self, base_url: str | None = None, timeout: int | None = None):
        self.base_url = (base_url or BASE_URL).rstrip("/")
        self.timeout = timeout or TIMEOUT
        self.session = requests.Session()

    def _sync_auth_cookie_header(self) -> None:
        """Force requests to send every ASP.NET auth cookie chunk.

        ASP.NET Core may split a large auth ticket into cookies like:
        TongjiForumAuth=chunks-2; TongjiForumAuthC1=...; TongjiForumAuthC2=...
        The normal cookie jar can be affected by local host/domain/secure rules in
        some Windows pytest environments, so we also maintain an explicit Cookie
        header containing all auth-cookie chunks.
        """
        auth_items: dict[str, str] = {}
        for cookie in self.session.cookies:
            if cookie.name.startswith("TongjiForumAuth") and cookie.value:
                # Deduplicate by cookie name. requests may keep both a host-only
                # cookie and a localhost-domain cookie after our fallback parsing.
                auth_items[cookie.name] = cookie.value

        # Stable order: base marker first, then C1/C2/...
        ordered_items = sorted(auth_items.items(), key=lambda item: (0 if item[0] == "TongjiForumAuth" else 1, item[0]))

        if ordered_items:
            self.session.headers.update({
                "Cookie": "; ".join(f"{name}={value}" for name, value in ordered_items)
            })
        else:
            self.session.headers.pop("Cookie", None)

    def _clear_auth_cookies(self) -> None:
        for cookie in list(self.session.cookies):
            if cookie.name.startswith("TongjiForumAuth"):
                self.session.cookies.clear(domain=cookie.domain, path=cookie.path, name=cookie.name)
        self.session.headers.pop("Cookie", None)

    def _remember_auth_cookie(self, response: requests.Response) -> None:
        set_cookie = response.headers.get("Set-Cookie", "")
        if "TongjiForumAuth" not in set_cookie and not any(
            cookie.name.startswith("TongjiForumAuth") for cookie in response.cookies
        ):
            return

        lowered = set_cookie.lower()
        if "tongjiforumauth=;" in lowered or "max-age=0" in lowered or "expires=thu, 01 jan 1970" in lowered:
            self._clear_auth_cookies()
            return

        # requests usually stores response cookies automatically in session.cookies.
        # This fallback handles combined Set-Cookie headers and chunked cookies.
        try:
            parsed = SimpleCookie()
            parsed.load(set_cookie)
            for name, morsel in parsed.items():
                if name.startswith("TongjiForumAuth") and morsel.value:
                    self.session.cookies.set(name, morsel.value, path="/", domain="localhost")
        except Exception:
            # Keep the normal requests cookie jar behavior if manual parsing fails.
            pass

        self._sync_auth_cookie_header()

    def _request(self, method: str, path: str, **kwargs) -> requests.Response:
        kwargs.setdefault("timeout", self.timeout)
        response = self.session.request(method, f"{self.base_url}{path}", **kwargs)
        self._remember_auth_cookie(response)
        if path.lower().endswith("/auth/logout"):
            self._clear_auth_cookies()
        return response

    def get(self, path: str, **kwargs) -> requests.Response:
        return self._request("GET", path, **kwargs)

    def post(self, path: str, **kwargs) -> requests.Response:
        return self._request("POST", path, **kwargs)

    def put(self, path: str, **kwargs) -> requests.Response:
        return self._request("PUT", path, **kwargs)

    def delete(self, path: str, **kwargs) -> requests.Response:
        return self._request("DELETE", path, **kwargs)

    def close(self):
        self.session.close()
