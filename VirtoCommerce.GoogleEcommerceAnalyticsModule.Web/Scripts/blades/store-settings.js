angular.module('virtoCommerce.googleEcommerceAnalyticsModule')
.controller('virtoCommerce.googleEcommerceAnalyticsModule.storeSettingsController', ['$scope', 'platformWebApp.bladeNavigationService', function ($scope, bladeNavigationService) {

    function initializeBlade(data) {
        $scope.blade.currentEntities = data;
        $scope.blade.isLoading = false;
    };

    $scope.selectNode = function (node) {
        $scope.selectedNodeId = node.code;

        var newBlade = {
            id: 'googleEcommerceStoreSettings',
            data: node,
            title: $scope.blade.title,
            subtitle: 'google-ecommerce-analytics.widgets.store-settings.blade-subtitle',
            controller: 'virtoCommerce.googleEcommerceAnalyticsModule.storeSettingsController',
            template: 'Modules/$(VirtoCommerce.GoogleEcommerceAnalytics)/Scripts/blades/storeSettings.tpl.html'
        };
        bladeNavigationService.showBlade(newBlade, $scope.blade);
    };

    $scope.sortableOptions = {
        stop: function (e, ui) {
            for (var i = 0; i < $scope.blade.currentEntities.length; i++) {
                $scope.blade.currentEntities[i].priority = i + 1;
            }
        },
        axis: 'y',
        cursor: "move"
    };

    $scope.blade.headIcon = 'fa-archive';

    $scope.$watch('blade.parentBlade.currentEntity.paymentMethods', initializeBlade);
}]);