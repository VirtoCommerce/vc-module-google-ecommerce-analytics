using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;

public interface IAnalyticsService
{
    Task<bool> IsConfiguredAsync(string storeId);

    // An item-scoped dimension (itemId / itemName / itemListName) switches the report to the item scope: Count
    // then means items viewed, not occurrences, and EventName is filled only when exactly one name was asked for.
    Task<AnalyticsEventSearchResult> SearchEventsAsync(AnalyticsEventSearchCriteria criteria);

    Task<IList<AnalyticsEventSummary>> GetEventSummariesAsync(AnalyticsEventSummaryCriteria criteria);
}
