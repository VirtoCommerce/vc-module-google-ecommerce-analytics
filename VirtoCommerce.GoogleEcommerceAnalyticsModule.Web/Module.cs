using System;
using System.Linq;
using Microsoft.Practices.Unity;
using VirtoCommerce.Domain.Order.Events;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Ovservers;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web
{
    public class Module : ModuleBase
    {
        private readonly IUnityContainer _container;

        public Module(IUnityContainer container)
        {
            _container = container;
        }

        #region Public Methods and Operators

        public override void Initialize()
        {
			_container.RegisterType<IGoogleAnalyticsSettingsManager, GoogleAnalyticsSettingsManager>();
			_container.RegisterType<IGoogleAnalyticsTransactionManager, GoogleAnalyticsTransactionManager>();

            _container.RegisterType<IObserver<OrderChangedEvent>, OrderChangedObserver>("OrderChangedObserver");
        }

        public override void PostInitialize()
        {
            base.PostInitialize();

            var settingManager = _container.Resolve<ISettingsManager>();
            var googleEcommerceAnalyticsSettings = settingManager.GetModuleSettings("VirtoCommerce.GoogleEcommerceAnalytics").ToArray();
            settingManager.RegisterModuleSettings("VirtoCommerce.Store", googleEcommerceAnalyticsSettings);
        }
        #endregion
    }
}
