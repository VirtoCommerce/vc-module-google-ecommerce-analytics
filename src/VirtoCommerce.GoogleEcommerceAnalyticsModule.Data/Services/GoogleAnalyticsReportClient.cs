using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json.Linq;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class GoogleAnalyticsReportClient : IGoogleAnalyticsReportClient
{
    private const string CredentialTypeProperty = "type";

    private static readonly string[] AnalyticsReadOnlyScopes = { "https://www.googleapis.com/auth/analytics.readonly" };

    public virtual Task ValidateCredentialAsync(string credentialJson)
    {
        return ResolveCredentialAsync(credentialJson);
    }

    public virtual async Task<Metadata> GetMetadataAsync(string credentialJson, string propertyId)
    {
        var client = await CreateClientAsync(credentialJson);
        return await client.GetMetadataAsync(new GetMetadataRequest { Name = $"properties/{propertyId}/metadata" });
    }

    public virtual async Task<RunReportResponse> RunReportAsync(string credentialJson, RunReportRequest request)
    {
        var client = await CreateClientAsync(credentialJson);
        return await client.RunReportAsync(request);
    }

    public virtual async Task<RunRealtimeReportResponse> RunRealtimeReportAsync(string credentialJson, RunRealtimeReportRequest request)
    {
        var client = await CreateClientAsync(credentialJson);
        return await client.RunRealtimeReportAsync(request);
    }

    public virtual async Task<CheckCompatibilityResponse> CheckCompatibilityAsync(string credentialJson, CheckCompatibilityRequest request)
    {
        var client = await CreateClientAsync(credentialJson);
        return await client.CheckCompatibilityAsync(request);
    }

    protected virtual async Task<BetaAnalyticsDataClient> CreateClientAsync(string credentialJson)
    {
        var builder = new BetaAnalyticsDataClientBuilder
        {
            GoogleCredential = await ResolveCredentialAsync(credentialJson),
        };

        return await builder.BuildAsync();
    }

    protected virtual async Task<GoogleCredential> ResolveCredentialAsync(string credentialJson)
    {
        var credential = string.IsNullOrWhiteSpace(credentialJson)
            ? await GetApplicationDefaultCredentialAsync()
            : CreateCredentialFromJson(credentialJson);

        return credential.IsCreateScopedRequired
            ? credential.CreateScoped(AnalyticsReadOnlyScopes)
            : credential;
    }

    // Supports both service_account keys and Workload Identity Federation external_account configurations;
    // the declared credential type is passed through because CredentialFactory has no multi-type overload.
    protected virtual GoogleCredential CreateCredentialFromJson(string credentialJson)
    {
        var credentialType = JObject.Parse(credentialJson)[CredentialTypeProperty]?.Value<string>();
        return CredentialFactory.FromJson(credentialJson, credentialType);
    }

    protected virtual Task<GoogleCredential> GetApplicationDefaultCredentialAsync()
    {
        return GoogleCredential.GetApplicationDefaultAsync();
    }
}
