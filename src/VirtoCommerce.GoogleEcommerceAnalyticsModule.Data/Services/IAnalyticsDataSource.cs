using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public interface IAnalyticsDataSource
{
    Task<AnalyticsEventSearchResult> GetRowsAsync(AnalyticsDataQuery query);
}
