using DataApiSettings = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.Settings.DataApi;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsDataApiSettings
{
    public string PropertyId { get; set; }

    // The descriptor's default: 0 now means "disabled", so an unset instance must not land on it.
    public int CacheTtlMinutes { get; set; } = DataApiSettings.DefaultCacheTtlMinutes;

    public bool IsConfigured => !string.IsNullOrEmpty(PropertyId);
}
