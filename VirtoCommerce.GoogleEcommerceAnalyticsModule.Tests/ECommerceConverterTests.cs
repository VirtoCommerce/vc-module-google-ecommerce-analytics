using System.IO;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Converters;
using VirtoCommerce.OrdersModule.Core.Model;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests
{
    public class ECommerceConverterTests
    {
        protected CustomerOrder GetOrder()
        {
            var customerObjectJson = System.IO.File.ReadAllText(Path.Combine("MoqData", "CustomerOrder.json"));

            return Newtonsoft.Json.JsonConvert.DeserializeObject<CustomerOrder>(customerObjectJson, new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All });
        }

        [Fact]
        public void LineItemToTransactionItem()
        {
            var customerOrder = GetOrder();

            foreach (var lineItem in customerOrder.Items)
            {
                var result = ECommerceConverter.LineItemToTransactionItem(customerOrder, lineItem, true);
                Assert.Equal(customerOrder.Number, result.TransactionId);
                Assert.Equal(customerOrder.CustomerId, result.ClientId);
                Assert.Equal(customerOrder.Currency, result.CurrencyCode);
                Assert.Equal(lineItem.CategoryId, result.ItemCategory);
                Assert.Equal(lineItem.Sku, result.ItemCode);
                Assert.Equal(lineItem.Name, result.ItemName);
                Assert.Equal(lineItem.PlacedPrice, result.ItemPrice);
                Assert.Equal(-1 * lineItem.Quantity, result.ItemQuantity);
                Assert.Equal(customerOrder.CustomerId, result.UserId);
            }
        }

        [Fact]
        public void LineItemToRevertTransactionItem()
        {
            var customerOrder = GetOrder();

            foreach (var lineItem in customerOrder.Items)
            {
                var result = ECommerceConverter.LineItemToTransactionItem(customerOrder, lineItem);
                Assert.Equal(customerOrder.Number, result.TransactionId);
                Assert.Equal(customerOrder.CustomerId, result.ClientId);
                Assert.Equal(customerOrder.Currency, result.CurrencyCode);
                Assert.Equal(lineItem.CategoryId, result.ItemCategory);
                Assert.Equal(lineItem.Sku, result.ItemCode);
                Assert.Equal(lineItem.Name, result.ItemName);
                Assert.Equal(lineItem.PlacedPrice, result.ItemPrice);
                Assert.Equal(lineItem.Quantity, result.ItemQuantity);
                Assert.Equal(customerOrder.CustomerId, result.UserId);
            }
        }

        [Fact]
        public void OrderToTransaction()
        {
            var customerOrder = GetOrder();

            var result = ECommerceConverter.OrderToTransaction(customerOrder);
            Assert.Equal(customerOrder.Number, result.TransactionId);
            Assert.Equal(customerOrder.CustomerId, result.ClientId);
            Assert.Equal(customerOrder.Currency, result.CurrencyCode);
            Assert.Equal(customerOrder.Total, result.TransactionRevenue);
            Assert.Equal(customerOrder.ShippingTotal, result.TransactionShipping);
            Assert.Equal(customerOrder.TaxTotal, result.TransactionTax);
            Assert.Equal(customerOrder.CustomerId, result.UserId);
        }

        [Fact]
        public void OrderToRevertTransaction()
        {
            var customerOrder = GetOrder();

            var result = ECommerceConverter.OrderToTransaction(customerOrder, true);
            Assert.Equal(customerOrder.Number, result.TransactionId);
            Assert.Equal(customerOrder.CustomerId, result.ClientId);
            Assert.Equal(customerOrder.Currency, result.CurrencyCode);
            Assert.Equal(-1 * customerOrder.Total, result.TransactionRevenue);
            Assert.Equal(-1 * customerOrder.ShippingTotal, result.TransactionShipping);
            Assert.Equal(-1 * customerOrder.TaxTotal, result.TransactionTax);
            Assert.Equal(customerOrder.CustomerId, result.UserId);
        }
    }
}