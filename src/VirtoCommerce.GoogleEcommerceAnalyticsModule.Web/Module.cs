using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
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
            serviceCollection.AddSingleton<IGoogleAnalyticsReportClient, GoogleAnalyticsReportClient>();
            serviceCollection.AddTransient<GoogleAnalyticsDataSource>();
            serviceCollection.AddTransient<SampleAnalyticsDataSource>();
            serviceCollection.AddTransient<IAnalyticsSettingsResolver, AnalyticsSettingsResolver>();
            serviceCollection.AddTransient<IAnalyticsService, AnalyticsService>();
            serviceCollection.AddTransient<IAnalyticsCompatibilityService, AnalyticsCompatibilityService>();

            AbstractTypeFactory<AnalyticsDimensionFilter>.RegisterType<AnalyticsDimensionFilter>();
            AbstractTypeFactory<AnalyticsEvent>.RegisterType<AnalyticsEvent>();
            AbstractTypeFactory<AnalyticsEventSearchCriteria>.RegisterType<AnalyticsEventSearchCriteria>();
            AbstractTypeFactory<AnalyticsEventSearchResult>.RegisterType<AnalyticsEventSearchResult>();
            AbstractTypeFactory<AnalyticsEventSummary>.RegisterType<AnalyticsEventSummary>();
            AbstractTypeFactory<AnalyticsEventSummaryCriteria>.RegisterType<AnalyticsEventSummaryCriteria>();
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var serviceProvider = appBuilder.ApplicationServices;

            // Register permissions
            var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
            permissionsRegistrar.RegisterPermissions(ModuleConstants.Security.Permissions.AllPermissions
                .Select(x => new Permission { ModuleId = ModuleInfo.Id, GroupName = "GoogleAnalytics4", Name = x })
                .ToArray());

            // register settings
            var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            //Register store level settings
            settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.StoreLevelSettings, nameof(Store));
        }

        public void Uninstall()
        {
            // do nothing in here
        }
    }
}
