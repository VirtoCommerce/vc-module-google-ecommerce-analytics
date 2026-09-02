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

        var result = AbstractTypeFactory<AnalyticsDataApiSettings>.TryCreateInstance();

        result.PropertyId = await GetSettingAsync<string>(storeSettings, DataApiSettings.PropertyId);
        result.CacheTtlMinutes = await GetSettingAsync<int>(storeSettings, DataApiSettings.CacheTtlMinutes);

        return result;
    }

    protected virtual async Task<T> GetSettingAsync<T>(ICollection<ObjectSettingEntry> storeSettings, SettingDescriptor descriptor)
    {
        return storeSettings?.Any(x => x.Name.EqualsIgnoreCase(descriptor.Name) && x.Value != null) == true
            ? storeSettings.GetValue<T>(descriptor)
            : await _settingsManager.GetValueAsync<T>(descriptor);
    }
}
