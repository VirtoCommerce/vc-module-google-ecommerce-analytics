using System;
using System.Globalization;
using System.Linq;
using GoogleAnalyticsTracker.Simple;
using VirtoCommerce.Domain.Order.Model;
using VirtoCommerce.Platform.Core.Settings;
using System.Threading.Tasks;
using System.Collections.Generic;
using GoogleAnalyticsTracker.Core.Interface;
using GoogleAnalyticsTracker.Core.TrackerParameters;
using GoogleAnalyticsTracker.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Converters;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
	

	public class GoogleAnalyticsTransactionManager : IGoogleAnalyticsTransactionManager
	{
		readonly IGoogleAnalyticsSettingsManager _settingsManager;

		public GoogleAnalyticsTransactionManager(IGoogleAnalyticsSettingsManager settingsManager)
		{
			if (settingsManager == null)
			{
				throw new ArgumentNullException(nameof(settingsManager));
			}

			_settingsManager = settingsManager;
		}

		public async Task CreateTransaction(CustomerOrder order)
		{
			if (order == null)
			{
				throw new ArgumentNullException(nameof(order));
			}

			var setings = _settingsManager.Get(order.StoreId);

			if (!setings.IsActive)
				return;

			using (var tracker = new SimpleTracker(setings.TrackingId, setings.TrackingDomain ?? string.Empty, CreateEnvironment()))
			{
				var list = new List<Task<TrackingResult>>();

				var task = tracker.TrackAsync(ECommerceConverter.OrderToTransaction(order));
				list.Add(task);

				foreach (var lineItem in order.Items)
				{
					var lineItemTask = tracker.TrackAsync(ECommerceConverter.LineItemToTransactionItem(order, lineItem));

					list.Add(lineItemTask);
				}

				await Task.WhenAll(list.ToArray());
			}
		}

		private ITrackerEnvironment CreateEnvironment()
		{
			return new SimpleTrackerEnvironment(
				Environment.OSVersion.Platform.ToString(),
				Environment.OSVersion.Version.ToString(),
				Environment.OSVersion.VersionString
				);
		}

		public async Task RevertTransaction(CustomerOrder order)
		{
			if (order == null)
			{
				throw new ArgumentNullException(nameof(order));
			}

			var setings = _settingsManager.Get(order.StoreId);

			if (!setings.IsActive)
				return;

			using (var tracker = new SimpleTracker(setings.TrackingId, setings.TrackingDomain ?? string.Empty, CreateEnvironment()))
			{
				var list = new List<Task<TrackingResult>>();

				var task = tracker.TrackAsync(ECommerceConverter.OrderToTransaction(order, true));
				list.Add(task);

				foreach (var lineItem in order.Items)
				{
					var lineItemTask = tracker.TrackAsync(ECommerceConverter.LineItemToTransactionItem(order, lineItem, true));
					list.Add(lineItemTask);
				}

				await Task.WhenAll(list.ToArray());
			}
		}
	}
}