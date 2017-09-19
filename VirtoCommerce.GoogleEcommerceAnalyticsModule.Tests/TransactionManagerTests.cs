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

		protected CustomerOrder GetOrder()
		{
			var customerObjectJson = System.IO.File.ReadAllText(@"MoqData\CustomerOrder.json");

			return Newtonsoft.Json.JsonConvert.DeserializeObject<CustomerOrder>(customerObjectJson);
		}

		private IGoogleAnalyticsSettingsManager GetSettingsManager()
		{
			return Mock.Of<IGoogleAnalyticsSettingsManager>(s => s.Get(It.IsAny<string>()) == 
				new GoogleAnalyticsSettings
				{
					IsActive = true,
					CreateECommerceTransaction = true,
					ReverseECommerceTransaction = true,
					TrackingId = _googleAnalyticsTrackingId
				});
		}


		[Fact]
		public void CreateTransaction()
		{
			IGoogleAnalyticsTransactionManager manager = new GoogleAnalyticsTransactionManager(GetSettingsManager());

			var customerOrder = GetOrder();
			customerOrder.Number = "DEMO" + DateTime.Now.ToString("s");
			var task = manager.CreateTransaction(customerOrder);
			task.Wait();
		}


		[Fact]
		public void RevertTransaction()
		{
			string revertOrderNumber = "DEMO2017-09-08T08:55:32";

			IGoogleAnalyticsTransactionManager manager = new GoogleAnalyticsTransactionManager(GetSettingsManager());

			var customerOrder = GetOrder();
			customerOrder.Number = revertOrderNumber;
			var task = manager.RevertTransaction(customerOrder);
			task.Wait();
		}

		[Fact]
		public void CreateAndRevertTransaction()
		{
			IGoogleAnalyticsTransactionManager manager = new GoogleAnalyticsTransactionManager(GetSettingsManager());

			var customerOrder = GetOrder();
			customerOrder.Number = "DEMOREVERT" + DateTime.Now.ToString("s");
			var task1 = manager.CreateTransaction(customerOrder);
			task1.Wait();

			var task2 = manager.RevertTransaction(customerOrder);
			task2.Wait();
		}

	}
}
