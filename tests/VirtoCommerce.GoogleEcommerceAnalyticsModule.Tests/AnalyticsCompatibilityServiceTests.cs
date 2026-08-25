using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Moq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

public class AnalyticsCompatibilityServiceTests
{
    private const string StoreId = "test-store";

    private readonly Mock<IAnalyticsSettingsResolver> _settingsResolverMock = new();
    private readonly Mock<IGoogleAnalyticsReportClient> _reportClientMock = new();
    private readonly List<CheckCompatibilityRequest> _capturedRequests = new();

    [Fact]
    public async Task CheckCompatibilityAsync_SampleDataEnabled_ReportsUnavailableWithoutCallingGoogle()
    {
        SetupSettings(new AnalyticsDataApiSettings { PropertyId = "123456", SampleDataEnabled = true });
        var service = CreateService();

        var result = await service.CheckCompatibilityAsync(StoreId);

        Assert.False(result.Available);
        Assert.Contains("Sample data", result.ErrorMessage);
        Assert.Empty(result.Reports);
        _reportClientMock.Verify(x => x.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CheckCompatibilityRequest>()), Times.Never);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_NoPropertyId_ReportsErrorWithoutCallingGoogle()
    {
        SetupSettings(new AnalyticsDataApiSettings());
        var service = CreateService();

        var result = await service.CheckCompatibilityAsync(StoreId);

        Assert.False(result.Available);
        Assert.Contains("not configured", result.ErrorMessage);
        Assert.Empty(result.Reports);
        _reportClientMock.Verify(x => x.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CheckCompatibilityRequest>()), Times.Never);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_ResolverThrows_ReturnsErrorPayload()
    {
        _settingsResolverMock
            .Setup(x => x.ResolveAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var service = CreateService();

        var result = await service.CheckCompatibilityAsync(StoreId);

        Assert.False(result.Available);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_BuildsCanonicalReportRequests()
    {
        SetupSettings(CreateGoogleSettings());
        SetupClient(new CheckCompatibilityResponse());
        var service = CreateService();

        var result = await service.CheckCompatibilityAsync(StoreId);

        Assert.True(result.Available);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(new[] { "searchTerms", "browsedProducts" }, result.Reports.Select(x => x.Report));
        Assert.All(result.Reports, x => Assert.True(x.Compatible));
        Assert.All(result.Reports, x => Assert.Empty(x.IncompatibleDimensions));
        Assert.All(result.Reports, x => Assert.Empty(x.IncompatibleMetrics));

        Assert.Equal(2, _capturedRequests.Count);

        var searchTermsRequest = _capturedRequests[0];
        Assert.Equal("properties/123456", searchTermsRequest.Property);
        Assert.Equal(Compatibility.Incompatible, searchTermsRequest.CompatibilityFilter);
        Assert.Equal(new[] { "eventName", "dateHour", "searchTerm" }, searchTermsRequest.Dimensions.Select(x => x.Name));
        Assert.Equal("eventCount", Assert.Single(searchTermsRequest.Metrics).Name);
        Assert.Equal(
            new[] { "eventName", "customUser:session_kind", "customUser:organization_id" },
            searchTermsRequest.DimensionFilter.AndGroup.Expressions.Select(x => x.Filter.FieldName));
        Assert.Equal(
            new[] { "search", "view_search_results" },
            searchTermsRequest.DimensionFilter.AndGroup.Expressions[0].Filter.InListFilter.Values);

        var browsedProductsRequest = _capturedRequests[1];
        Assert.Equal("properties/123456", browsedProductsRequest.Property);
        Assert.Equal(new[] { "dateHour", "itemId", "itemName" }, browsedProductsRequest.Dimensions.Select(x => x.Name));
        Assert.Equal("itemsViewed", Assert.Single(browsedProductsRequest.Metrics).Name);
        Assert.Equal(
            new[] { "view_item" },
            browsedProductsRequest.DimensionFilter.AndGroup.Expressions[0].Filter.InListFilter.Values);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_MapsIncompatibleFieldsIntersectedWithRequested()
    {
        SetupSettings(CreateGoogleSettings());

        var response = new CheckCompatibilityResponse();
        response.DimensionCompatibilities.Add(new DimensionCompatibility
        {
            Compatibility = Compatibility.Incompatible,
            DimensionMetadata = new DimensionMetadata { ApiName = "customUser:organization_id" },
        });
        response.DimensionCompatibilities.Add(new DimensionCompatibility
        {
            Compatibility = Compatibility.Incompatible,
            DimensionMetadata = new DimensionMetadata { ApiName = "unrelatedDimension" },
        });
        response.MetricCompatibilities.Add(new MetricCompatibility
        {
            Compatibility = Compatibility.Incompatible,
            MetricMetadata = new MetricMetadata { ApiName = "itemsViewed" },
        });
        SetupClient(response);
        var service = CreateService();

        var result = await service.CheckCompatibilityAsync(StoreId);

        var searchTerms = result.Reports.First(x => x.Report == "searchTerms");
        Assert.False(searchTerms.Compatible);
        Assert.Equal(new[] { "customUser:organization_id" }, searchTerms.IncompatibleDimensions);
        Assert.Empty(searchTerms.IncompatibleMetrics);

        var browsedProducts = result.Reports.First(x => x.Report == "browsedProducts");
        Assert.False(browsedProducts.Compatible);
        Assert.Equal(new[] { "customUser:organization_id" }, browsedProducts.IncompatibleDimensions);
        Assert.Equal(new[] { "itemsViewed" }, browsedProducts.IncompatibleMetrics);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_ClientThrows_SetsReportErrorMessageInsteadOfFailing()
    {
        SetupSettings(CreateGoogleSettings());
        _reportClientMock
            .Setup(x => x.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CheckCompatibilityRequest>()))
            .ThrowsAsync(new InvalidOperationException("credentials missing"));
        var service = CreateService();

        var result = await service.CheckCompatibilityAsync(StoreId);

        Assert.True(result.Available);
        Assert.Equal(2, result.Reports.Count);
        Assert.All(result.Reports, x =>
        {
            Assert.False(x.Compatible);
            Assert.Equal("credentials missing", x.ErrorMessage);
        });
    }

    private AnalyticsCompatibilityService CreateService()
    {
        return new AnalyticsCompatibilityService(
            _settingsResolverMock.Object,
            new GoogleAnalyticsDataSource(_reportClientMock.Object),
            _reportClientMock.Object);
    }

    private void SetupSettings(AnalyticsDataApiSettings settings)
    {
        _settingsResolverMock
            .Setup(x => x.ResolveAsync(StoreId))
            .ReturnsAsync(settings);
    }

    private void SetupClient(CheckCompatibilityResponse response)
    {
        _reportClientMock
            .Setup(x => x.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CheckCompatibilityRequest>()))
            .Callback((string _, CheckCompatibilityRequest request) => _capturedRequests.Add(request))
            .ReturnsAsync(response);
    }

    private static AnalyticsDataApiSettings CreateGoogleSettings()
    {
        return new AnalyticsDataApiSettings { PropertyId = "123456", CredentialJson = "{}" };
    }
}
