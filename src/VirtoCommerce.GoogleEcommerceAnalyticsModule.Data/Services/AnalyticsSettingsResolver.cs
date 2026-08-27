using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public AnalyticsSettingsResolver(IStoreService storeService, ISettingsManager settingsManager)
    {
        _storeService = storeService;
        _settingsManager = settingsManager;
    }

    public virtual async Task<AnalyticsDataApiSettings> ResolveAsync(string storeId)
    {
        var store = string.IsNullOrEmpty(storeId) ? null : await _storeService.GetNoCloneAsync(storeId);
        var storeSettings = store?.Settings;

        return new AnalyticsDataApiSettings
        {
            PropertyId = await GetSettingAsync<string>(storeSettings, DataApiSettings.PropertyId),
            CredentialJson = await GetSettingAsync<string>(storeSettings, DataApiSettings.ServiceAccountJson),
            CacheTtlMinutes = await GetSettingAsync<int>(storeSettings, DataApiSettings.CacheTtlMinutes),
        };
    }

    protected virtual async Task<T> GetSettingAsync<T>(ICollection<ObjectSettingEntry> storeSettings, SettingDescriptor descriptor)
    {
        var storeEntry = storeSettings?.FirstOrDefault(x => x.Name.EqualsIgnoreCase(descriptor.Name));
        if (storeEntry?.Value != null)
        {
            return storeSettings.GetValue<T>(descriptor);
        }

        return await _settingsManager.GetValueAsync<T>(descriptor);
    }
}
