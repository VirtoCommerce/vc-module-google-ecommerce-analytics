using DataApiSettings = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.Settings.DataApi;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsDataApiSettings
{
    public string PropertyId { get; set; }

    // The descriptor's default, so an instance built without one caches rather than reading 0 as "disabled".
    public int CacheTtlMinutes { get; set; } = DataApiSettings.DefaultCacheTtlMinutes;

    public bool IsConfigured => !string.IsNullOrEmpty(PropertyId);
}
