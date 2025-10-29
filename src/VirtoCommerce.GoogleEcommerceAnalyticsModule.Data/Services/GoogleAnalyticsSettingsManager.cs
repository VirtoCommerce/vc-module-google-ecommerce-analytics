using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;
using GoogleSettings = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
    public class GoogleAnalyticsSettingsManager : IGoogleAnalyticsSettingsManager
    {
        readonly IStoreService _storeService;

        public GoogleAnalyticsSettingsManager(IStoreService storeService)
        {
            _storeService = storeService;
        }

        public virtual async Task<GoogleAnalyticsSettings> GetAsync(string storeId)
        {
            var retVal = new GoogleAnalyticsSettings { EnableTracking = false };

            var store = await _storeService.GetNoCloneAsync(storeId);
            if (store == null)
            {
                return retVal;
            }

            retVal.MeasurementId = store.Settings.GetValue<string>(GoogleSettings.MeasurementId);
            retVal.GTMContainerId = store.Settings.GetValue<string>(GoogleSettings.GTMContainerId);

            // Enable tracking if either MeasurementId or GTMContainerId is provided
            if (!string.IsNullOrEmpty(retVal.MeasurementId) || !string.IsNullOrEmpty(retVal.GTMContainerId))
            {
                retVal.EnableTracking = store.Settings.GetValue<bool>(GoogleSettings.EnableTracking);
            }

            return retVal;
        }
    }
}
