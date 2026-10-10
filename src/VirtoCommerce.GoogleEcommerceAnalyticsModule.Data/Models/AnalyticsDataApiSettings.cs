using System;
using DataApiSettings = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.Settings.DataApi;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsDataApiSettings
{
    public string PropertyId { get; set; }

    // The descriptor's default: 0 now means "disabled", so an unset instance must not land on it.
    public int CacheTtlMinutes { get; set; } = DataApiSettings.DefaultCacheTtlMinutes;

    public int RequestTimeoutSeconds { get; set; } = DataApiSettings.DefaultRequestTimeoutSeconds;

    // No "disabled" here: a call without a deadline is one Google can hold for as long as it likes.
    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(
        RequestTimeoutSeconds > 0 ? RequestTimeoutSeconds : DataApiSettings.DefaultRequestTimeoutSeconds);

    // Whitespace is not a property id: a value of spaces would otherwise report as configured and build
    // "properties/ " for every read.
    public bool IsConfigured => !string.IsNullOrWhiteSpace(PropertyId);
}
