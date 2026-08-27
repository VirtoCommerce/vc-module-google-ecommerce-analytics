namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsDataApiSettings
{
    public string PropertyId { get; set; }

    public int CacheTtlMinutes { get; set; }

    public bool IsConfigured => !string.IsNullOrEmpty(PropertyId);
}
