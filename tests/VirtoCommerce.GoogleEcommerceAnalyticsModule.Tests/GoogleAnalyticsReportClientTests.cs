using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
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

        public Task<GoogleCredential> ResolveAsync()
        {
            return ResolveCredentialAsync();
        }

        protected override Task<GoogleCredential> GetApplicationDefaultCredentialAsync()
        {
            AdcCallCount++;
            return Task.FromResult(_applicationDefaultCredential);
        }
    }
}
