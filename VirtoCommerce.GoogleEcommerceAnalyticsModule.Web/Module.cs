using System.Linq;
using VirtoCommerce.Platform.Core.Modularity;
using Microsoft.Practices.Unity;
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

        public override void PostInitialize()
        {
            base.PostInitialize();

            var settingManager = _container.Resolve<ISettingsManager>();
            var googleEcommerceAnalyticsSettings = settingManager.GetModuleSettings("VirtoCommerce.GoogleEcommerceAnalytics").ToArray();
            settingManager.RegisterModuleSettings("VirtoCommerce.Store", googleEcommerceAnalyticsSettings);
        }
    }
}
