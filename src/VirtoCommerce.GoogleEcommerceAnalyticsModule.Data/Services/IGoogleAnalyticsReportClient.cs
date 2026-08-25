using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public interface IGoogleAnalyticsReportClient
{
    Task<RunReportResponse> RunReportAsync(string credentialJson, RunReportRequest request);

    Task<CheckCompatibilityResponse> CheckCompatibilityAsync(string credentialJson, CheckCompatibilityRequest request);
}
