using System;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.OrdersModule.Core.Model;
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
            var result = new Mock<IGoogleAnalyticsSettingsManager>();
            result.Setup(x => x.GetAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(new GoogleAnalyticsSettings
                {
                    IsActive = true,
                    CreateECommerceTransaction = true,
                    ReverseECommerceTransaction = true,
                    TrackingId = _googleAnalyticsTrackingId
                }));

            return result.Object;
        }


        [Fact]
        public async Task CreateTransaction()
        {
            var manager = new GoogleAnalyticsTransactionManager(GetSettingsManager());

            var customerOrder = GetOrder();
            customerOrder.Number = "DEMO" + DateTime.Now.ToString("s");
            await manager.CreateTransactionAsync(customerOrder);
        }


        [Fact]
        public async Task RevertTransaction()
        {
            string revertOrderNumber = "DEMO2017-09-08T08:55:32";

            IGoogleAnalyticsTransactionManager manager = new GoogleAnalyticsTransactionManager(GetSettingsManager());

            var customerOrder = GetOrder();
            customerOrder.Number = revertOrderNumber;
            await manager.RevertTransactionAsync(customerOrder);
        }

        [Fact]
        public async Task CreateAndRevertTransaction()
        {
            IGoogleAnalyticsTransactionManager manager = new GoogleAnalyticsTransactionManager(GetSettingsManager());

            var customerOrder = GetOrder();
            customerOrder.Number = "DEMOREVERT" + DateTime.Now.ToString("s");
            await manager.CreateTransactionAsync(customerOrder);

            await manager.RevertTransactionAsync(customerOrder);
        }
    }
}
