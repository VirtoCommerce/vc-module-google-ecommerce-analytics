using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.Domain.Store.Services;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
	public class GoogleAnalyticsSettingsManager : IGoogleAnalyticsSettingsManager
	{
		readonly IStoreService _storeService;

		public GoogleAnalyticsSettingsManager(IStoreService storeService)
		{
			_storeService = storeService;
		}

		public GoogleAnalyticsSettings Get(string storeId)
		{
			var retVal = new GoogleAnalyticsSettings();

			var store = _storeService.GetById(storeId);
			if (store == null)
				return retVal;

			retVal.TrackingId = store.Settings.GetSettingValue("GoogleEcommerceAnalytics.GoogleAnalyticsTrackingId", string.Empty);
			if (!string.IsNullOrEmpty(retVal.TrackingId))
			{
				retVal.IsActive = store.Settings.GetSettingValue("GoogleEcommerceAnalytics.EnableTracking", false);
			}

			return retVal;
		}
	}
}
