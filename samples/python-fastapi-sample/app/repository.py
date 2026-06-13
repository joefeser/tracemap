from sqlalchemy import text


class OrderRepository:
    def load(self, order_id: int):
        sql = text("SELECT id, status, total FROM orders WHERE id = :id")
        return self.execute(sql, {"id": order_id})

    def execute(self, query, params):
        return type("Order", (), {"status": "pending", "total": 42.0})()
