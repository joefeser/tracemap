from fastapi import APIRouter

from app.clients import notify_status
from app.models import OrderResponse
from app.repository import OrderRepository
from app.services import calculate_total

router = APIRouter(prefix="/orders")


@router.get("/{order_id}")
async def get_order(order_id: int) -> OrderResponse:
    repo = OrderRepository()
    order = repo.load(order_id)
    total = calculate_total(order)
    notify_status(order.status)
    return OrderResponse(id=order_id, status=order.status, total=total)
