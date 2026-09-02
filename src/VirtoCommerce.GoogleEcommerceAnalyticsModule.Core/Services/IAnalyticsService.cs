using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;

public interface IAnalyticsService
{
    Task<bool> IsConfiguredAsync(string storeId);

    Task<AnalyticsEventSearchResult> SearchEventsAsync(AnalyticsEventSearchCriteria criteria);

    Task<IList<AnalyticsEventSummary>> GetEventSummariesAsync(AnalyticsEventSummaryCriteria criteria);
}
