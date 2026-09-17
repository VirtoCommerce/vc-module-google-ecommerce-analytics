using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;
using DataApiSettings = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.Settings.DataApi;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class AnalyticsSettingsResolver : IAnalyticsSettingsResolver
{
    private readonly IStoreService _storeService;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<AnalyticsSettingsResolver> _logger;

    public AnalyticsSettingsResolver(
        IStoreService storeService,
        ISettingsManager settingsManager,
        ILogger<AnalyticsSettingsResolver> logger)
    {
        _storeService = storeService;
        _settingsManager = settingsManager;
        _logger = logger;
    }

    public virtual async Task<AnalyticsDataApiSettings> ResolveAsync(string storeId)
    {
        var store = string.IsNullOrEmpty(storeId) ? null : await _storeService.GetNoCloneAsync(storeId);
        var storeSettings = store?.Settings;

        var result = AbstractTypeFactory<AnalyticsDataApiSettings>.TryCreateInstance();

        result.PropertyId = await GetSettingAsync<string>(storeSettings, DataApiSettings.PropertyId);
        result.CacheTtlMinutes = await GetSettingAsync<int>(storeSettings, DataApiSettings.CacheTtlMinutes);

        return result;
    }

    protected virtual async Task<T> GetSettingAsync<T>(ICollection<ObjectSettingEntry> storeSettings, SettingDescriptor descriptor)
    {
        var hasStoreValue = storeSettings?.Any(x =>
            x.Name.EqualsIgnoreCase(descriptor.Name) && IsStoreOverride(x.Value)) == true;

        if (!hasStoreValue)
        {
            return await _settingsManager.GetValueAsync<T>(descriptor);
        }

        try
        {
            return storeSettings.GetValue<T>(descriptor);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            // A misconfigured value is not a reason to fail the read.
            _logger.LogWarning(ex, "Store setting {Setting} is not a valid {Type}; falling back to the global value",
                descriptor.Name, typeof(T).Name);

            return await _settingsManager.GetValueAsync<T>(descriptor);
        }
    }

    // Not just null: the admin UI writes "" for a cleared ShortText and a value of spaces is the same gesture,
    // so taking either as an override would disable reporting rather than restore the global value. Non-text
    // settings keep the plain null check — 0 is a legitimate CacheTtlMinutes.
    private static bool IsStoreOverride(object value)
    {
        return value is string text ? !string.IsNullOrWhiteSpace(text) : value is not null;
    }
}
