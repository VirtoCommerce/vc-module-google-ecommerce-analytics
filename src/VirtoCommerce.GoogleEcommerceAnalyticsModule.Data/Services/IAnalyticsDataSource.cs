using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

// The seam the GA4 implementation sits behind: subclass it to reshape a report, or double it in tests. Not a
// provider boundary, deliberately — AnalyticsDataQuery is GA4-shaped (a numeric property id, GA4 apiNames
// as dimension names) and another source would need query fields this one has no place for.
public interface IAnalyticsDataSource
{
    Task<AnalyticsEventSearchResult> GetRowsAsync(AnalyticsDataQuery query);
}
