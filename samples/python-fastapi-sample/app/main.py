from fastapi import FastAPI

from app.api.routes.orders import router

app = FastAPI()
app.include_router(router, prefix="/api")
