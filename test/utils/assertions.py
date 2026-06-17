import requests


def assert_success(response: requests.Response) -> dict:
    assert response.status_code == 200, f"Expected 200, got {response.status_code}: {response.text}"
    data = response.json()
    assert data["success"] is True, f"Expected success=true, got: {data}"
    return data


def assert_failure(response: requests.Response, expected_status: int = 400, message: str | None = None) -> dict:
    assert response.status_code == expected_status, \
        f"Expected {expected_status}, got {response.status_code}: {response.text}"
    data = response.json()
    if "success" in data:
        assert data["success"] is False, f"Expected success=false, got: {data}"
        if message is not None:
            assert data["message"] == message, f"Expected message='{message}', got: '{data['message']}'"
    else:
        assert "errors" in data, f"Expected 'success' or 'errors' in response, got: {data}"
    return data
