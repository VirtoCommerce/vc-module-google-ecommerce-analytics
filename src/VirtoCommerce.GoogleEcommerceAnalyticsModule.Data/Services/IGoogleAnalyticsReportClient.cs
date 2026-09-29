using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public interface IGoogleAnalyticsReportClient
{
    Task ValidateCredentialAsync();

    Task<Metadata> GetMetadataAsync(string propertyId);

    Task<RunReportResponse> RunReportAsync(RunReportRequest request);

    Task<RunRealtimeReportResponse> RunRealtimeReportAsync(RunRealtimeReportRequest request);

    Task<CheckCompatibilityResponse> CheckCompatibilityAsync(CheckCompatibilityRequest request);
}
