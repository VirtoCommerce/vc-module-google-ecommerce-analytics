angular.module('virtoCommerce.googleEcommerceAnalyticsModule')
.controller('virtoCommerce.googleEcommerceAnalyticsModule.storeSettingsController', ['$scope', 'platformWebApp.bladeNavigationService', 'virtoCommerce.googleEcommerceAnalyticsModule.settings', function ($scope, bladeNavigationService, storeSettings) {
    var blade = $scope.blade;

    blade.initialize = function () {
        blade.isLoading = true;
        $scope.blade.currentSettings = { storeId: blade.storeId };

        storeSettings.get({ storeId: blade.storeId }, function (data) {
            if (data.id) {
                $scope.blade.currentSettings = data;
            }
            $scope.blade.isLoading = false;
        },
        function (error) { bladeNavigationService.setError('Error ' + error.status, $scope.blade); });
    };

    $scope.saveChanges = function() {
        blade.isLoading = true;
        storeSettings.update({ storeId: blade.storeId }, blade.currentSettings, function() {
                $scope.bladeClose();
            },
            function (error) {
                bladeNavigationService.setError('Error ' + error.status, blade);
            });
    };

    $scope.cancelChanges = function () {
        $scope.bladeClose();
    }

    $scope.blade.headIcon = 'fa-database';

    blade.initialize();
}]);