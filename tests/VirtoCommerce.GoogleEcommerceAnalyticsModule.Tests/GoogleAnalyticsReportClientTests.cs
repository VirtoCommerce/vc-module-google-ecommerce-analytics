using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

public class GoogleAnalyticsReportClientTests
{
    private const string AnalyticsReadOnlyScope = "https://www.googleapis.com/auth/analytics.readonly";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveCredentialAsync_NoJson_UsesApplicationDefaultCredentials(string credentialJson)
    {
        var client = new TestableReportClient();

        var credential = await client.ResolveAsync(credentialJson);

        Assert.Equal(1, client.AdcCallCount);
        Assert.IsType<ServiceAccountCredential>(credential.UnderlyingCredential);
        Assert.Contains(AnalyticsReadOnlyScope, ((ServiceAccountCredential)credential.UnderlyingCredential).Scopes);
    }

    [Fact]
    public async Task ResolveCredentialAsync_ServiceAccountJson_CreatesScopedServiceAccountCredential()
    {
        var client = new TestableReportClient();

        var credential = await client.ResolveAsync(CreateServiceAccountJson());

        Assert.Equal(0, client.AdcCallCount);
        var serviceAccountCredential = Assert.IsType<ServiceAccountCredential>(credential.UnderlyingCredential);
        Assert.Contains(AnalyticsReadOnlyScope, serviceAccountCredential.Scopes);
        Assert.False(credential.IsCreateScopedRequired);
    }

    [Fact]
    public async Task ResolveCredentialAsync_ExternalAccountJson_CreatesWorkloadIdentityFederationCredential()
    {
        var client = new TestableReportClient();

        var credential = await client.ResolveAsync(CreateExternalAccountJson());

        Assert.Equal(0, client.AdcCallCount);
        Assert.NotNull(credential.UnderlyingCredential);
        Assert.Contains("ExternalAccountCredential", credential.UnderlyingCredential.GetType().Name);
        Assert.False(credential.IsCreateScopedRequired);
    }

    private static string CreateServiceAccountJson()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = PemEncoding.WriteString("PRIVATE KEY", rsa.ExportPkcs8PrivateKey());

        return JsonConvert.SerializeObject(new
        {
            type = "service_account",
            project_id = "test-project",
            private_key_id = "key1",
            private_key = privateKey,
            client_email = "probe@test-project.iam.gserviceaccount.com",
            client_id = "1",
            token_uri = "https://oauth2.googleapis.com/token",
        });
    }

    private static string CreateExternalAccountJson()
    {
        return JsonConvert.SerializeObject(new
        {
            type = "external_account",
            audience = "//iam.googleapis.com/projects/123/locations/global/workloadIdentityPools/pool/providers/azure",
            subject_token_type = "urn:ietf:params:oauth:token-type:jwt",
            token_url = "https://sts.googleapis.com/v1/token",
            credential_source = new { url = "https://login.example.com/token" },
        });
    }

    private sealed class TestableReportClient : GoogleAnalyticsReportClient
    {
        public int AdcCallCount { get; private set; }

        public Task<GoogleCredential> ResolveAsync(string credentialJson)
        {
            return ResolveCredentialAsync(credentialJson);
        }

        protected override Task<GoogleCredential> GetApplicationDefaultCredentialAsync()
        {
            AdcCallCount++;
            return Task.FromResult(CredentialFactory.FromJson(CreateServiceAccountJson(), JsonCredentialParameters.ServiceAccountCredentialType));
        }
    }
}
