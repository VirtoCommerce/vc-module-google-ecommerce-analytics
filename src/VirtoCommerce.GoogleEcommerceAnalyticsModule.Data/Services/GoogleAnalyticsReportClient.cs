using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Apis.Auth.OAuth2;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class GoogleAnalyticsReportClient : IGoogleAnalyticsReportClient
{
    private static readonly string[] AnalyticsReadOnlyScopes = { "https://www.googleapis.com/auth/analytics.readonly" };

    // This client is a singleton and BetaAnalyticsDataClient is thread-safe. An explicitly supplied
    // GoogleCredential opts the builder out of the shared GAX channel pool, so building per call would leak
    // an undisposed gRPC channel and start every call with a cold OAuth token cache.
    private Lazy<Task<GoogleCredential>> _credential;
    private Lazy<Task<BetaAnalyticsDataClient>> _client;

    public GoogleAnalyticsReportClient()
    {
        ResetCache();
    }

    public virtual Task ValidateCredentialAsync()
    {
        return GetCredentialAsync();
    }

    public virtual async Task<Metadata> GetMetadataAsync(string propertyId, TimeSpan timeout)
    {
        var client = await GetClientAsync();
        return await client.GetMetadataAsync(new GetMetadataRequest { Name = $"properties/{propertyId}/metadata" }, CreateCallSettings(timeout));
    }

    public virtual async Task<RunReportResponse> RunReportAsync(RunReportRequest request, TimeSpan timeout)
    {
        var client = await GetClientAsync();
        return await client.RunReportAsync(request, CreateCallSettings(timeout));
    }

    public virtual async Task<RunRealtimeReportResponse> RunRealtimeReportAsync(RunRealtimeReportRequest request, TimeSpan timeout)
    {
        var client = await GetClientAsync();
        return await client.RunRealtimeReportAsync(request, CreateCallSettings(timeout));
    }

    public virtual async Task<CheckCompatibilityResponse> CheckCompatibilityAsync(CheckCompatibilityRequest request, TimeSpan timeout)
    {
        var client = await GetClientAsync();
        return await client.CheckCompatibilityAsync(request, CreateCallSettings(timeout));
    }

    // Overlays the library's per-method default (60 s, never retried) with the store's deadline.
    protected virtual CallSettings CreateCallSettings(TimeSpan timeout)
    {
        return CallSettings.FromExpiration(Expiration.FromTimeout(timeout));
    }

    protected virtual Task<BetaAnalyticsDataClient> GetClientAsync()
    {
        return AwaitOrResetAsync(_client);
    }

    protected virtual Task<GoogleCredential> GetCredentialAsync()
    {
        return AwaitOrResetAsync(_credential);
    }

    protected virtual async Task<BetaAnalyticsDataClient> CreateClientAsync()
    {
        var builder = new BetaAnalyticsDataClientBuilder
        {
            GoogleCredential = await GetCredentialAsync(),
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

    // A faulted Lazy would replay the same failure for the lifetime of this singleton, which would leave
    // diagnostics permanently red after the operator fixes the credentials.
    private async Task<T> AwaitOrResetAsync<T>(Lazy<Task<T>> lazy)
    {
        try
        {
            return await lazy.Value;
        }
        catch
        {
            ResetCache();
            throw;
        }
    }

    private void ResetCache()
    {
        _credential = new Lazy<Task<GoogleCredential>>(ResolveCredentialAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        _client = new Lazy<Task<BetaAnalyticsDataClient>>(CreateClientAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}
