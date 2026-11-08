exampleApp
.factory("formatting", function () { return {}; })
.service("records", ["$filter", "$http", function (format, send) {
    this.submit = function (items) {
        return send({ method: "post", url: "/Portal/Desk/API/save.ashx", data: items });
    };
    this.list = function () { return send({ method: "GET", url: "/Portal/Desk/Api/list.ashx" }); };
}]);
exampleApp.service("unrelated", ["$http", function (send) {
    this.submit = function (items) {
        return send({ method: "POST", url: "/Portal/Desk/Api/unused.ashx", data: items });
    };
}]);
