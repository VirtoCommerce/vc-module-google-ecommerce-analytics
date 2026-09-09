using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

// The provider seam behind IAnalyticsService: register a different implementation to answer the same queries
// from another source (a first-party event store, a BigQuery export) without changing a consumer.
public interface IAnalyticsDataSource
{
    Task<AnalyticsEventSearchResult> GetRowsAsync(AnalyticsDataQuery query);
}
