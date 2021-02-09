angular.module('virtoCommerce.googleEcommerceAnalyticsModule')
.controller('virtoCommerce.googleEcommerceAnalyticsModule.storeSettingsController', ['$scope', function ($scope) {
    var blade = $scope.blade;
    blade.updatePermission = 'platform:setting:update';

    function initializeBlade(data) {
        blade.currentEntities = angular.copy(data);
        blade.origEntity = data;
        blade.refresh();
        blade.isLoading = false;
    }

    blade.refresh = function() {
        blade.enableTracking = _.find(blade.currentEntities, function (x) { return x.name === 'GoogleEcommerceAnalytics.EnableTracking'; });
        blade.createECommerceTransaction = _.find(blade.currentEntities, function (x) { return x.name === 'GoogleEcommerceAnalytics.CreateECommerceTransaction'; });
        blade.reverseECommerceTransaction = _.find(blade.currentEntities, function (x) { return x.name === 'GoogleEcommerceAnalytics.ReverseECommerceTransaction'; });

        blade.googleTagManagerId = _.find(blade.currentEntities, function (x) { return x.name === 'GoogleEcommerceAnalytics.GoogleTagManagerId'; });
        blade.googleAnalyticsTrackingId = _.find(blade.currentEntities, function (x) { return x.name === 'GoogleEcommerceAnalytics.GoogleAnalyticsTrackingId'; });
    };

    function isDirty() {
        return !angular.equals(blade.currentEntities, blade.origEntity) && blade.hasUpdatePermission();
    }

    $scope.saveChanges = function () {
        angular.copy(blade.currentEntities, blade.origEntity);
        $scope.bladeClose();
    };

    $scope.cancelChanges = function () {
        $scope.bladeClose();
    };

    $scope.blade.headIcon = 'fa fa-database';

    blade.toolbarCommands = [
        {
            name: "platform.commands.reset", icon: 'fa fa-undo',
            executeMethod: function () {
                angular.copy(blade.origEntity, blade.currentEntities);
                blade.refresh();
            },
            canExecuteMethod: isDirty,
            permission: blade.updatePermission
        }
    ];

    $scope.$watch('blade.parentBlade.currentEntity.settings', initializeBlade);
}]);