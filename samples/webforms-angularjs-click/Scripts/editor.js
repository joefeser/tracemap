sampleApp.controller("EditorController", ["$scope", function (view) {
    view.save = function (item) {
        return item;
    };
}])
.controller("ListController", ["$scope", function (scope) {
    scope.save = function () {};
}])
.controller("DraftController", ["$scope", function (scope) {
    scope.save = function () {};
}]);
