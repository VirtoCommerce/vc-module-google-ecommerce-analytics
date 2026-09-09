using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;

public interface IAnalyticsService
{
    Task<bool> IsConfiguredAsync(string storeId);

    // Naming an item-scoped dimension (itemId / itemName / itemListName) switches the report to the item scope:
    // the metric becomes items viewed rather than event occurrences, so AnalyticsEvent.Count means that instead —
    // and EventName is only filled in when the criteria named exactly one.
    Task<AnalyticsEventSearchResult> SearchEventsAsync(AnalyticsEventSearchCriteria criteria);

    Task<IList<AnalyticsEventSummary>> GetEventSummariesAsync(AnalyticsEventSummaryCriteria criteria);
}
