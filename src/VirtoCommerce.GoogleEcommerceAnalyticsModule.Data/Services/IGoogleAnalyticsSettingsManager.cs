using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
	public class GoogleAnalyticsSettings
	{
		public bool EnableTracking { get; set; }
		public string MeasurementId { get; set; }
	}

	public interface IGoogleAnalyticsSettingsManager
	{
		Task<GoogleAnalyticsSettings> GetAsync(string storeId);
	}
}
