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

            retVal.MeasurementId = store.Settings.GetSettingValue(GoogleSettings.MeasurementId.Name, string.Empty);
            if (!string.IsNullOrEmpty(retVal.MeasurementId))
            {
                retVal.EnableTracking = store.Settings.GetSettingValue(GoogleSettings.EnableTracking.Name, false);
            }

            return retVal;
        }
    }
}
