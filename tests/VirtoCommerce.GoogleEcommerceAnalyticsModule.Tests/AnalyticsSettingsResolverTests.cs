using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

public class AnalyticsSettingsResolverTests
{
    private const string StoreId = "test-store";

    private readonly Mock<IStoreService> _storeServiceMock = new();
    private readonly Mock<ISettingsManager> _settingsManagerMock = new();

    public AnalyticsSettingsResolverTests()
    {
        _settingsManagerMock
            .Setup(x => x.GetObjectSettingAsync(It.IsAny<string>(), null, null))
            .ReturnsAsync((string name, string _, string _) => new ObjectSettingEntry { Name = name });
    }

    [Fact]
    public async Task ResolveAsync_MapsStoreSettings()
    {
        SetupStore(CreateStore(
            (ModuleConstants.Settings.DataApi.PropertyId.Name, "123456"),
            (ModuleConstants.Settings.DataApi.CacheTtlMinutes.Name, 15)));
        var resolver = CreateResolver();

        var settings = await resolver.ResolveAsync(StoreId);

        Assert.Equal("123456", settings.PropertyId);
        Assert.Equal(15, settings.CacheTtlMinutes);
        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public async Task ResolveAsync_StoreValueWinsOverGlobal()
    {
        SetupGlobalSetting(ModuleConstants.Settings.DataApi.PropertyId.Name, "global-property");
        SetupStore(CreateStore((ModuleConstants.Settings.DataApi.PropertyId.Name, "store-property")));
        var resolver = CreateResolver();

        var settings = await resolver.ResolveAsync(StoreId);

        Assert.Equal("store-property", settings.PropertyId);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToGlobalSettings()
    {
        SetupGlobalSetting(ModuleConstants.Settings.DataApi.PropertyId.Name, "global-property");
        SetupStore(CreateStore());
        var resolver = CreateResolver();

        var settings = await resolver.ResolveAsync(StoreId);

        Assert.Equal("global-property", settings.PropertyId);
    }

    [Fact]
    public async Task ResolveAsync_NullStoreId_UsesGlobalSettingsWithoutLoadingStore()
    {
        SetupGlobalSetting(ModuleConstants.Settings.DataApi.PropertyId.Name, "global-property");
        var resolver = CreateResolver();

        var settings = await resolver.ResolveAsync(null);

        Assert.Equal("global-property", settings.PropertyId);
        _storeServiceMock.Verify(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_NothingConfigured_ReturnsUnconfiguredSettings()
    {
        SetupStore(CreateStore());
        var resolver = CreateResolver();

        var settings = await resolver.ResolveAsync(StoreId);

        Assert.Null(settings.PropertyId);
        Assert.False(settings.IsConfigured);
    }

    private AnalyticsSettingsResolver CreateResolver()
    {
        return new AnalyticsSettingsResolver(_storeServiceMock.Object, _settingsManagerMock.Object);
    }

    private void SetupStore(Store store)
    {
        _storeServiceMock
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) =>
                ids.Contains(store.Id) ? new List<Store> { store } : new List<Store>());
    }

    private void SetupGlobalSetting(string name, object value)
    {
        _settingsManagerMock
            .Setup(x => x.GetObjectSettingAsync(name, null, null))
            .ReturnsAsync(new ObjectSettingEntry { Name = name, Value = value });
    }

    private static Store CreateStore(params (string Name, object Value)[] settings)
    {
        return new Store
        {
            Id = StoreId,
            Settings = settings.Select(x => new ObjectSettingEntry { Name = x.Name, Value = x.Value }).ToList(),
        };
    }
}
