from app import app

route_name = "/dynamic/" + "value"
app.add_url_rule(route_name, "dynamic", lambda: "dynamic")
