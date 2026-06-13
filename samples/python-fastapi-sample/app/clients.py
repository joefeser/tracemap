import requests


def notify_status(status: str) -> None:
    requests.post("https://example.test/api/order-status", json={"status": status})
