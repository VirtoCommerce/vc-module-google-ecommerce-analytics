using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.Domain.Order.Model;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.Platform.Core.Settings;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests
{
    public class TransactionManagerTests
    {
		public readonly string _googleAnalyticsTrackingId = "UA-88035702-1";

		protected CustomerOrder CreateOrder()
		{
			var customerObjectJson = System.IO.File.ReadAllText(@"MoqData\CustomerOrder.json");

			return Newtonsoft.Json.JsonConvert.DeserializeObject<CustomerOrder>(customerObjectJson);
		}

		[Fact]
		public void CreateTransaction()
		{
			ISettingsManager settingsManaher = Mock.Of<ISettingsManager>(s => s.GetValue(It.IsAny<string>(), string.Empty) == _googleAnalyticsTrackingId);

			IGoogleAnalyticsTransactionManager manager = new GoogleAnalyticsTransactionManager(settingsManaher);

			var customerOrder = CreateOrder();
			customerOrder.Number = "DEMO" + DateTime.Now.ToString("s");
			var task = manager.CreateTransaction(customerOrder);
			task.Wait();
		}


		[Fact]
		public void RevertTransaction()
		{
			string revertOrderNumber = "DEMO2017-09-07T18:24:35";

			ISettingsManager settingsManaher = Mock.Of<ISettingsManager>(s => s.GetValue(It.IsAny<string>(), string.Empty) == _googleAnalyticsTrackingId);
			IGoogleAnalyticsTransactionManager manager = new GoogleAnalyticsTransactionManager(settingsManaher);

			var customerOrder = CreateOrder();
			customerOrder.Number = revertOrderNumber;
			var task = manager.RevertTransaction(customerOrder);
			task.Wait();
		}

	}
}
