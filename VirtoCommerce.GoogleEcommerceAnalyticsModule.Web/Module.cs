using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Handlers;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.Platform.Core.Bus;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web
{
    public class Module : IModule
    {
        public ManifestModuleInfo ModuleInfo { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IGoogleAnalyticsSettingsManager, GoogleAnalyticsSettingsManager>();
            serviceCollection.AddTransient<IGoogleAnalyticsTransactionManager, GoogleAnalyticsTransactionManager>();

            serviceCollection.AddTransient<OrderChangedHandler>();
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            // register settings
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
            //Register store level settings
            settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.AllSettings, nameof(Store));

            var inProcessBus = appBuilder.ApplicationServices.GetService<IHandlerRegistrar>();
            inProcessBus.RegisterHandler<OrderChangedEvent>((message, token) => appBuilder.ApplicationServices.GetService<OrderChangedHandler>().Handle(message));
        }

        public void Uninstall()
        {
            // do nothing in here
        }
    }
}
