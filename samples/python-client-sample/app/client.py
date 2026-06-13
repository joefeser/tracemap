import requests


def get_order(order_id: int):
    return requests.get("https://example.test/api/orders/{order_id}", params={"id": order_id})
