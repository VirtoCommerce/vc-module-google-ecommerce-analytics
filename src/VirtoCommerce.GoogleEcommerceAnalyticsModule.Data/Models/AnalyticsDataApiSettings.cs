using DataApiSettings = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.Settings.DataApi;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsDataApiSettings
{
    public string PropertyId { get; set; }

    // The descriptor's default: 0 now means "disabled", so an unset instance must not land on it.
    public int CacheTtlMinutes { get; set; } = DataApiSettings.DefaultCacheTtlMinutes;

    // Whitespace is not a property id: a value of spaces would otherwise report as configured and build
    // "properties/ " for every read.
    public bool IsConfigured => !string.IsNullOrWhiteSpace(PropertyId);
}
