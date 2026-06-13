import httpx


def call_remote(status: str):
    return httpx.get("https://example.test/api/status/" + status)
