angular.module("example", []).controller("OtherPanel", function ($scope) {
    $scope.save = function () {};
}).controller("Panel", ["$scope", "records", function (view, records) {
    records.list().then(function (rows) {
        view.items = rows;
        view.save = function () { return view.items; };
    });
}]);
