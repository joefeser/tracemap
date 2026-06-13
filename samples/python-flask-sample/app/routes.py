from app import app
from app.clients import call_remote


@app.route("/api/status/<status>", methods=["GET"])
def status(status: str):
    call_remote(status)
    return {"status": status}
