namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsDataApiSettings
{
    public string PropertyId { get; set; }

    public string CredentialJson { get; set; }

    public int CacheTtlMinutes { get; set; }

    public bool SampleDataEnabled { get; set; }

    public bool IsGoogleConfigured => !string.IsNullOrEmpty(PropertyId);
}
