using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;

public interface IAnalyticsDiagnosticsService
{
    Task<AnalyticsDiagnosticsResult> RunAsync(string storeId, AnalyticsDiagnosticsRequest request);
}
