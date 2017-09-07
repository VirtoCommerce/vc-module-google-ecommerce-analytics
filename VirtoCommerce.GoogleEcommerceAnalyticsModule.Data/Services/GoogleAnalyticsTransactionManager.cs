using System;
using System.Globalization;
using System.Linq;
using GoogleAnalyticsTracker.Simple;
using VirtoCommerce.Domain.Order.Model;
using VirtoCommerce.Platform.Core.Settings;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
    public class GoogleAnalyticsTransactionManager : IGoogleAnalyticsTransactionManager
    {
        private readonly string _trackingId;

        public GoogleAnalyticsTransactionManager(ISettingsManager settingsManager)
        {
            _trackingId = settingsManager.GetValue("GoogleEcommerceAnalytics.GoogleAnalyticsTrackingId", string.Empty);
        }

		public void CreateTransaction(CustomerOrder order)
		{
			if (order == null)
			{
				throw new ArgumentNullException(nameof(order));
			}

			var address = order.Addresses.FirstOrDefault(a => a.AddressType == Domain.Commerce.Model.AddressType.Shipping);
			if (address == null)
			{
				address = order.Addresses.FirstOrDefault(a => a.AddressType == Domain.Commerce.Model.AddressType.Billing);
			}

			using (var tracker = new SimpleTracker(_trackingId, string.Empty))
			{
				var list = new List<Task>();

				var task = tracker.TrackTransactionAsync(
					orderId: order.Number,
					storeName: string.Empty,
					total: (order.Total).ToString(CultureInfo.InvariantCulture),
					tax: (order.TaxTotal).ToString(CultureInfo.InvariantCulture),
					shipping: (order.ShippingTotal).ToString(CultureInfo.InvariantCulture),
					city: address?.City,
					region: address?.RegionName,
					country: address?.CountryName);
				list.Add(task);

				foreach (var lineItem in order.Items)
				{
					var lineItemTask = tracker.TrackTransactionItemAsync(
						orderId: order.Number,
						productId: lineItem.ProductId,
						productName: lineItem.Name,
						productVariation: lineItem.Sku,
						productPrice: lineItem.PlacedPrice.ToString(CultureInfo.InvariantCulture),
						quantity: (lineItem.Quantity).ToString(CultureInfo.InvariantCulture));
					Task.WaitAll(list.ToArray());
				}
			}
		}


		public void RevertTransaction(CustomerOrder order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            var address = order.Addresses.FirstOrDefault(a => a.AddressType == Domain.Commerce.Model.AddressType.Shipping);
            if (address == null)
            {
                address = order.Addresses.FirstOrDefault(a => a.AddressType == Domain.Commerce.Model.AddressType.Billing);
            }

            using (var tracker = new SimpleTracker(_trackingId, string.Empty))
            {
				var list = new List<Task>();

				var task = tracker.TrackTransactionAsync(
                    orderId: order.Number,
                    storeName: string.Empty,
                    total: (-1 * order.Total).ToString(CultureInfo.InvariantCulture),
                    tax: (-1 * order.TaxTotal).ToString(CultureInfo.InvariantCulture),
                    shipping: (-1 * order.ShippingTotal).ToString(CultureInfo.InvariantCulture),
                    city: address?.City,
                    region: address?.RegionName,
                    country: address?.CountryName);
				list.Add(task);


				foreach (var lineItem in order.Items)
                {
                    var lineItemTask = tracker.TrackTransactionItemAsync(
                        orderId: order.Number,
                        productId: lineItem.ProductId,
                        productName: lineItem.Name,
                        productVariation: lineItem.Sku,
                        productPrice: lineItem.PlacedPrice.ToString(CultureInfo.InvariantCulture),
                        quantity: (-1 * lineItem.Quantity).ToString(CultureInfo.InvariantCulture));
					list.Add(lineItemTask);
				}

				Task.WaitAll(list.ToArray());
            }
        }
    }
}