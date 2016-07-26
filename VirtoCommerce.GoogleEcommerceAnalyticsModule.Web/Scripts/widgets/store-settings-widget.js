angular.module('virtoCommerce.googleEcommerceAnalyticsModule')
.controller('virtoCommerce.googleEcommerceAnalyticsModule.storeSettingsWidgetController', ['$scope', 'platformWebApp.bladeNavigationService', function ($scope, bladeNavigationService) {
    var blade = $scope.widget.blade;
    
    $scope.openBlade = function () {
        var newBlade = {
            id: "storeChildBlade",
            storeId: blade.currentEntityId,
            title: blade.title,
            subtitle: 'google-ecommerce-analytics.widgets.store-settings.blade-subtitle',
            controller: 'virtoCommerce.googleEcommerceAnalyticsModule.storeSettingsController',
            template: 'Modules/$(VirtoCommerce.GoogleEcommerceAnalytics)/Scripts/blades/store-settings.tpl.html',
            securityScopes: blade.securityScopes
        };
        bladeNavigationService.showBlade(newBlade, blade);
    };
}]);