//Call this to register our module to main application
var moduleName = "virtoCommerce.googleEcommerceAnalyticsModule";

if (AppDependencies != undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
.run(['platformWebApp.widgetService', 
	function (widgetService) {
	    // themes widget in STORE details
	    widgetService.registerWidget({
	        controller: 'virtoCommerce.googleEcommerceAnalyticsModule.storeSettingsWidgetController',
	        template: 'Modules/$(VirtoCommerce.GoogleEcommerceAnalytics)/Scripts/widgets/store-settings-widget.tpl.html',
	        permission: 'content:read'
	    }, 'storeDetail');
	}]);
