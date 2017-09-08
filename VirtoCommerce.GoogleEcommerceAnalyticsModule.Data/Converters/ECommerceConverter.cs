using GoogleAnalyticsTracker.Core.TrackerParameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.Domain.Order.Model;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Converters
{
	public static class ECommerceConverter
	{
		public static ECommerceItem LineItemToTransactionItem(CustomerOrder order, LineItem lineItem, bool isRevert = false)
		{
			if (order == null)
				throw new ArgumentNullException(nameof(order));
			if (lineItem == null)
				throw new ArgumentNullException(nameof(lineItem));

			int revertPrefix = isRevert ? -1 : 1;

			return new ECommerceItem
			{
				TransactionId = order.Number,
				ClientId = order.CustomerId,
				CurrencyCode = order.Currency,
				ItemCategory = lineItem.CategoryId,
				ItemCode = lineItem.Sku,
				ItemName = lineItem.Name,
				ItemPrice = lineItem.PlacedPrice,
				ItemQuantity = revertPrefix * lineItem.Quantity,
				UserId = order.CustomerId
			};
		}

		public static ECommerceTransaction OrderToTransaction(CustomerOrder order, bool isRevert = false)
		{
			if (order == null)
				throw new ArgumentNullException(nameof(order));

			decimal revertPrefix = isRevert ? -1 : 1;

			return new ECommerceTransaction
			{
				ClientId = order.CustomerId,
				CurrencyCode = order.Currency,
				TransactionId = order.Number,
				TransactionRevenue = revertPrefix * order.Total,
				TransactionShipping = revertPrefix * order.ShippingTotal,
				TransactionTax = revertPrefix * order.TaxTotal,
				UserId = order.CustomerId
			};
		}
	}
}
