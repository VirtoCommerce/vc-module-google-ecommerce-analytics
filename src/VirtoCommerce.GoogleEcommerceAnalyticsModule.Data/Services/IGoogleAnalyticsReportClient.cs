using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public interface IGoogleAnalyticsReportClient
{
    Task ValidateCredentialAsync(string credentialJson);

    Task<Metadata> GetMetadataAsync(string credentialJson, string propertyId);

    Task<RunReportResponse> RunReportAsync(string credentialJson, RunReportRequest request);

    Task<RunRealtimeReportResponse> RunRealtimeReportAsync(string credentialJson, RunRealtimeReportRequest request);

    Task<CheckCompatibilityResponse> CheckCompatibilityAsync(string credentialJson, CheckCompatibilityRequest request);
}
