using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
	public class GoogleAnalyticsSettings
	{
		public string TrackingId { get; set; }
		public bool IsActive { get; set; }
		public string TrackingDomain { get; set; }

		public bool CreateECommerceTransaction { get; set; }
		public bool ReverseECommerceTransaction { get; set; }
	}

	public interface IGoogleAnalyticsSettingsManager
	{
		GoogleAnalyticsSettings Get(string storeId);
	}
}
