using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Google.Api.Gax.Grpc;
using Google.Apis.Auth.OAuth2;
using Moq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

public class GoogleAnalyticsReportClientTests
{
    private const string AnalyticsReadOnlyScope = "https://www.googleapis.com/auth/analytics.readonly";
    private const int PrivateKeySizeInBits = 2048;

    [Fact]
    public async Task ResolveCredentialAsync_UsesApplicationDefaultCredentialsScopedForAnalytics()
    {
        var client = new TestableReportClient(CreateUnscopedCredential());

        var credential = await client.ResolveAsync();

        Assert.Equal(1, client.AdcCallCount);
        var serviceAccountCredential = Assert.IsType<ServiceAccountCredential>(credential.UnderlyingCredential);
        Assert.Contains(AnalyticsReadOnlyScope, serviceAccountCredential.Scopes);
    }

    [Fact]
    public async Task ResolveCredentialAsync_AlreadyScopedCredential_ReturnsItUnchanged()
    {
        var scopedCredential = CreateUnscopedCredential().CreateScoped(AnalyticsReadOnlyScope);
        var client = new TestableReportClient(scopedCredential);

        var credential = await client.ResolveAsync();

        Assert.Equal(1, client.AdcCallCount);
        Assert.Same(scopedCredential, credential);
        Assert.False(credential.IsCreateScopedRequired);
    }

    // The client is a singleton and caches the credential in a Lazy. A faulted Lazy replays its failure
    // forever, so a credential that fails once would leave diagnostics red even after the operator fixes ADC.
    [Fact]
    public async Task GetCredentialAsync_AfterAFailedResolve_ResolvesAgainInsteadOfReplayingTheFailure()
    {
        var client = new TestableReportClient(CreateUnscopedCredential()) { FailNextAdcCall = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetCachedCredentialAsync());
        var credential = await client.GetCachedCredentialAsync();

        Assert.NotNull(credential);
        Assert.Equal(2, client.AdcCallCount);
    }

    [Fact]
    public async Task GetCredentialAsync_AfterASuccessfulResolve_IsCached()
    {
        var client = new TestableReportClient(CreateUnscopedCredential());

        var first = await client.GetCachedCredentialAsync();
        var second = await client.GetCachedCredentialAsync();

        Assert.Same(first, second);
        Assert.Equal(1, client.AdcCallCount);
    }

    // The library's own default is 60 seconds, never retried; the store's deadline has to reach the call itself.
    [Fact]
    public async Task RunReportAsync_SendsTheTimeoutAsTheCallDeadline()
    {
        CallSettings callSettings = null;
        var dataClientMock = new Mock<BetaAnalyticsDataClient>();
        dataClientMock
            .Setup(x => x.RunReportAsync(It.IsAny<RunReportRequest>(), It.IsAny<CallSettings>()))
            .Callback((RunReportRequest _, CallSettings settings) => callSettings = settings)
            .ReturnsAsync(new RunReportResponse());
        var client = new TestableReportClient(CreateUnscopedCredential()) { DataClient = dataClientMock.Object };

        await client.RunReportAsync(new RunReportRequest(), TimeSpan.FromSeconds(45));

        Assert.Equal(TimeSpan.FromSeconds(45), callSettings.Expiration.Timeout);
    }

    private static GoogleCredential CreateUnscopedCredential()
    {
        using var rsa = RSA.Create(PrivateKeySizeInBits);
        var privateKey = PemEncoding.WriteString("PRIVATE KEY", rsa.ExportPkcs8PrivateKey());

        var initializer = new ServiceAccountCredential.Initializer("probe@test-project.iam.gserviceaccount.com")
        {
            ProjectId = "test-project",
        }.FromPrivateKey(privateKey);

        return GoogleCredential.FromServiceAccountCredential(new ServiceAccountCredential(initializer));
    }

    private sealed class TestableReportClient : GoogleAnalyticsReportClient
    {
        private readonly GoogleCredential _applicationDefaultCredential;

        public TestableReportClient(GoogleCredential applicationDefaultCredential)
        {
            _applicationDefaultCredential = applicationDefaultCredential;
        }

        public int AdcCallCount { get; private set; }

        public bool FailNextAdcCall { get; set; }

        public BetaAnalyticsDataClient DataClient { get; set; }

        public Task<GoogleCredential> ResolveAsync()
        {
            return ResolveCredentialAsync();
        }

        // The cached path, the one a singleton actually takes on every call.
        public Task<GoogleCredential> GetCachedCredentialAsync()
        {
            return GetCredentialAsync();
        }

        protected override Task<BetaAnalyticsDataClient> GetClientAsync()
        {
            return DataClient != null ? Task.FromResult(DataClient) : base.GetClientAsync();
        }

        protected override Task<GoogleCredential> GetApplicationDefaultCredentialAsync()
        {
            AdcCallCount++;

            if (FailNextAdcCall)
            {
                FailNextAdcCall = false;
                throw new InvalidOperationException("The Application Default Credentials are not available.");
            }

            return Task.FromResult(_applicationDefaultCredential);
        }
    }
}
