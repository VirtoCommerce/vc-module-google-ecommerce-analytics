using System;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public interface IGoogleAnalyticsReportClient
{
    Task ValidateCredentialAsync();

    Task<Metadata> GetMetadataAsync(string propertyId, TimeSpan timeout);

    Task<RunReportResponse> RunReportAsync(RunReportRequest request, TimeSpan timeout);

    Task<RunRealtimeReportResponse> RunRealtimeReportAsync(RunRealtimeReportRequest request, TimeSpan timeout);

    Task<CheckCompatibilityResponse> CheckCompatibilityAsync(CheckCompatibilityRequest request, TimeSpan timeout);
}
