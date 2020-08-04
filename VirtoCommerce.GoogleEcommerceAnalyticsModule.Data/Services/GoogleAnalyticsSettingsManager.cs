using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;

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
            var retVal = new GoogleAnalyticsSettings();

            var store = await _storeService.GetByIdAsync(storeId);
            if (store == null)
                return retVal;

            retVal.TrackingId = store.Settings.GetSettingValue("GoogleEcommerceAnalytics.GoogleAnalyticsTrackingId", string.Empty);
            if (!string.IsNullOrEmpty(retVal.TrackingId))
            {
                retVal.IsActive = store.Settings.GetSettingValue("GoogleEcommerceAnalytics.EnableTracking", false);
            }

            retVal.CreateECommerceTransaction = store.Settings.GetSettingValue("GoogleEcommerceAnalytics.CreateECommerceTransaction", false);
            retVal.ReverseECommerceTransaction = store.Settings.GetSettingValue("GoogleEcommerceAnalytics.ReverseECommerceTransaction", true);

            return retVal;
        }
    }
}
