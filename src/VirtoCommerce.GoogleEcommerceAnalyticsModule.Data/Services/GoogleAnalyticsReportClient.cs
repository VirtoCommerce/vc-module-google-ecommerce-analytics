using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Google.Apis.Auth.OAuth2;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class GoogleAnalyticsReportClient : IGoogleAnalyticsReportClient
{
    private static readonly string[] AnalyticsReadOnlyScopes = { "https://www.googleapis.com/auth/analytics.readonly" };

    public virtual Task ValidateCredentialAsync()
    {
        return ResolveCredentialAsync();
    }

    public virtual async Task<Metadata> GetMetadataAsync(string propertyId)
    {
        var client = await CreateClientAsync();
        return await client.GetMetadataAsync(new GetMetadataRequest { Name = $"properties/{propertyId}/metadata" });
    }

    public virtual async Task<RunReportResponse> RunReportAsync(RunReportRequest request)
    {
        var client = await CreateClientAsync();
        return await client.RunReportAsync(request);
    }

    public virtual async Task<RunRealtimeReportResponse> RunRealtimeReportAsync(RunRealtimeReportRequest request)
    {
        var client = await CreateClientAsync();
        return await client.RunRealtimeReportAsync(request);
    }

    public virtual async Task<CheckCompatibilityResponse> CheckCompatibilityAsync(CheckCompatibilityRequest request)
    {
        var client = await CreateClientAsync();
        return await client.CheckCompatibilityAsync(request);
    }

    protected virtual async Task<BetaAnalyticsDataClient> CreateClientAsync()
    {
        var builder = new BetaAnalyticsDataClientBuilder
        {
            GoogleCredential = await ResolveCredentialAsync(),
        };

        return await builder.BuildAsync();
    }

    protected virtual async Task<GoogleCredential> ResolveCredentialAsync()
    {
        var credential = await GetApplicationDefaultCredentialAsync();

        return credential.IsCreateScopedRequired
            ? credential.CreateScoped(AnalyticsReadOnlyScopes)
            : credential;
    }

    protected virtual Task<GoogleCredential> GetApplicationDefaultCredentialAsync()
    {
        return GoogleCredential.GetApplicationDefaultAsync();
    }
}
