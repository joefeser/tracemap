exampleApp.controller("OtherPanel", function ($scope) {
    $scope.save = function () {};
}).controller("Panel", ["$scope", "records", function (view, client) {
    client.list().then(function (rows) {
        view.save = function () {
            client.submit(rows).then(function () {});
        };
    }).catch(function (error) { view.notice = error; });
}]);
