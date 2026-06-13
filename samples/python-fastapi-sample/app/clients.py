import requests as rq
from httpx import get as http_get


class StatusPublisher:
    def __init__(self, client):
        self.client = client

    def publish(self, status: str):
        return self.client.send(status)


def notify_status(status: str) -> None:
    rq.post("https://example.test/api/order-status", json={"status": status})
    http_get("https://example.test/api/status")
    publish_audit(status)


def publish_audit(status: str) -> str:
    return status
