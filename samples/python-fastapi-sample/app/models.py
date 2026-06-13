from typing import Annotated

from pydantic import BaseModel, Field
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column


class OrderResponse(BaseModel):
    id: int
    status: Annotated[str, Field(description="Current order status")]
    total: float


class Base(DeclarativeBase):
    pass


class OrderRecord(Base):
    __tablename__ = "orders"

    id: Mapped[int] = mapped_column(primary_key=True)
    status: Mapped[str] = mapped_column()
    total: Mapped[float] = mapped_column()
