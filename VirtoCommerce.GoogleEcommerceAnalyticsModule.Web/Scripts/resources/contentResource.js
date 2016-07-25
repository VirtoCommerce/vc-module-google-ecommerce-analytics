angular.module('virtoCommerce.googleEcommerceAnalyticsModule')
.factory('virtoCommerce.googleEcommerceAnalyticsModule.settings', ['$resource', function ($resource) {
    return $resource('api/ga/:storeId/settings/', {}, {
        get: { url: 'api/ga/:storeId/settings/', method: 'GET', isArray: false},
        update: { url: 'api/cms/:storeId/settings/', method: 'POST' }
    });
}])