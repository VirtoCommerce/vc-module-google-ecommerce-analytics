using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
    public class GoogleAnalyticsSettingsManager : IGoogleAnalyticsSettingsManager
    {
        readonly ICrudService<Store> _storeService;

        public GoogleAnalyticsSettingsManager(ICrudService<Store> storeService)
        {
            _storeService = storeService;
        }

        public virtual async Task<GoogleAnalyticsSettings> GetAsync(string storeId)
        {
            var retVal = new GoogleAnalyticsSettings { EnableTracking = false};

            var store = await _storeService.GetByIdAsync(storeId);
            if (store == null)
                return retVal;

            retVal.MeasurementId = store.Settings.GetSettingValue("GoogleAnalytics4.MeasurementId", string.Empty);
            if (!string.IsNullOrEmpty(retVal.MeasurementId))
            {
                retVal.EnableTracking = store.Settings.GetSettingValue("GoogleAnalytics4.EnableTracking", false);
            }

            return retVal;
        }
    }
}
