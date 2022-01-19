using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleAnalyticsTracker.Core;
using GoogleAnalyticsTracker.Core.Interface;
using GoogleAnalyticsTracker.Simple;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Converters;
using VirtoCommerce.OrdersModule.Core.Model;

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

        public async Task CreateTransactionAsync(CustomerOrder order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            var settings = await _settingsManager.GetAsync(order.StoreId);

            if (!settings.IsActive || !settings.CreateECommerceTransaction)
                return;

            using (var tracker = new SimpleTracker(settings.TrackingId, CreateEnvironment()))
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

        public async Task RevertTransactionAsync(CustomerOrder order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            var settings = await _settingsManager.GetAsync(order.StoreId);

            if (!settings.IsActive || !settings.ReverseECommerceTransaction)
                return;

            using (var tracker = new SimpleTracker(settings.TrackingId, CreateEnvironment()))
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