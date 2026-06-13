from os import environ, getenv

ORDER_QUEUE = getenv("ORDER_QUEUE", "orders")
API_TOKEN = environ["API_TOKEN"]
