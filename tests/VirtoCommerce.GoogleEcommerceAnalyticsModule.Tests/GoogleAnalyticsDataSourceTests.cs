using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Moq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

public class GoogleAnalyticsDataSourceTests
{
    private static readonly string[] ExpectedSearchReportDimensions = { "eventName", "dateHour", "searchTerm", "customUser:organization_id" };
    private static readonly string[] ExpectedSearchCountDimensions = { "eventName", "searchTerm" };
    private static readonly string[] ExpectedItemReportDimensions = { "dateHour", "itemId", "itemName" };
    private static readonly string[] ExpectedItemCountDimensions = { "itemId", "itemName" };
    private static readonly string[] ExpectedSearchEventFilterValues = { "search" };
    private static readonly string[] ExpectedViewItemEventFilterValues = { "view_item" };
    private static readonly string[] ExpectedOrganizationFilterValues = { "org1", "org2" };

    private readonly Mock<IGoogleAnalyticsReportClient> _reportClientMock = new();

    private RunReportRequest _capturedRequest;

    [Fact]
    public async Task GetRowsAsync_BuildsRunReportRequest()
    {
        var dataSource = CreateDataSource();
        var query = new AnalyticsDataQuery
        {
            PropertyId = "123456",
            EventNames = new List<string> { ModuleConstants.EventNames.Search },
            DimensionNames = new List<string> { ModuleConstants.Dimensions.SearchTerm, ModuleConstants.UserDimensions.OrganizationId },
            DimensionFilters = new List<AnalyticsDimensionFilter>
            {
                new() { DimensionName = ModuleConstants.UserDimensions.OrganizationId, Values = new List<string> { "org1", "org2" } },
                new() { DimensionName = ModuleConstants.Dimensions.SearchTerm, Values = new List<string> { "drill" } },
            },
            From = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc),
            Take = 10,
            Skip = 5,
        };

        await dataSource.GetRowsAsync(query);

        Assert.Equal("properties/123456", _capturedRequest.Property);
        Assert.Equal(10L, _capturedRequest.Limit);
        Assert.Equal(5L, _capturedRequest.Offset);

        Assert.Equal(
            ExpectedSearchReportDimensions,
            _capturedRequest.Dimensions.Select(x => x.Name));
        Assert.Equal("eventCount", Assert.Single(_capturedRequest.Metrics).Name);

        var dateRange = Assert.Single(_capturedRequest.DateRanges);
        Assert.Equal("2026-08-01", dateRange.StartDate);
        Assert.Equal("2026-08-25", dateRange.EndDate);

        var expressions = _capturedRequest.DimensionFilter.AndGroup.Expressions;
        Assert.Equal(3, expressions.Count);
        Assert.Equal("eventName", expressions[0].Filter.FieldName);
        Assert.Equal(ExpectedSearchEventFilterValues, expressions[0].Filter.InListFilter.Values);
        Assert.Equal("customUser:organization_id", expressions[1].Filter.FieldName);
        Assert.Equal(ExpectedOrganizationFilterValues, expressions[1].Filter.InListFilter.Values);
        Assert.Equal("searchTerm", expressions[2].Filter.FieldName);

        var orderBy = Assert.Single(_capturedRequest.OrderBys);
        Assert.True(orderBy.Desc);
        Assert.Equal("dateHour", orderBy.Dimension.DimensionName);
    }

    [Fact]
    public async Task GetRowsAsync_NoFromTo_UsesDefaultDateRange()
    {
        var dataSource = CreateDataSource();

        await dataSource.GetRowsAsync(new AnalyticsDataQuery { PropertyId = "123456", Take = 10 });

        var dateRange = Assert.Single(_capturedRequest.DateRanges);
        Assert.Equal("2015-08-14", dateRange.StartDate);
        Assert.Equal("today", dateRange.EndDate);
    }

    [Fact]
    public async Task GetRowsAsync_SingleFilter_DoesNotWrapInAndGroup()
    {
        var dataSource = CreateDataSource();

        await dataSource.GetRowsAsync(new AnalyticsDataQuery
        {
            PropertyId = "123456",
            EventNames = new List<string> { ModuleConstants.EventNames.Login },
            Take = 10,
        });

        Assert.Null(_capturedRequest.DimensionFilter.AndGroup);
        Assert.Equal("eventName", _capturedRequest.DimensionFilter.Filter.FieldName);
    }

    [Fact]
    public async Task GetRowsAsync_NoFilters_LeavesDimensionFilterEmpty()
    {
        var dataSource = CreateDataSource();

        await dataSource.GetRowsAsync(new AnalyticsDataQuery { PropertyId = "123456", Take = 10 });

        Assert.Null(_capturedRequest.DimensionFilter);
    }

    [Fact]
    public async Task GetRowsAsync_MapsRowsAndNormalizesNotSet()
    {
        var response = new RunReportResponse { RowCount = 42 };
        response.DimensionHeaders.Add(new DimensionHeader { Name = "eventName" });
        response.DimensionHeaders.Add(new DimensionHeader { Name = "dateHour" });
        response.DimensionHeaders.Add(new DimensionHeader { Name = "searchTerm" });
        response.DimensionHeaders.Add(new DimensionHeader { Name = "customUser:organization_id" });

        var row = new Row();
        row.DimensionValues.Add(new DimensionValue { Value = "search" });
        row.DimensionValues.Add(new DimensionValue { Value = "2026082510" });
        row.DimensionValues.Add(new DimensionValue { Value = "drill" });
        row.DimensionValues.Add(new DimensionValue { Value = "(not set)" });
        row.MetricValues.Add(new MetricValue { Value = "7" });
        response.Rows.Add(row);

        var dataSource = CreateDataSource(response);

        var result = await dataSource.GetRowsAsync(new AnalyticsDataQuery { PropertyId = "123456", Take = 10 });

        Assert.Equal(42, result.TotalCount);

        var analyticsEvent = Assert.Single(result.Events);
        Assert.Equal("search", analyticsEvent.EventName);
        Assert.Equal(new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc), analyticsEvent.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, analyticsEvent.OccurredAt.Value.Kind);
        Assert.Equal(7, analyticsEvent.Count);
        Assert.Equal("drill", analyticsEvent.Dimensions["searchTerm"]);
        Assert.False(analyticsEvent.Dimensions.ContainsKey("organization_id"));
    }

    [Fact]
    public async Task GetRowsAsync_UserDimensionPrefixStrippedInRows()
    {
        var response = new RunReportResponse { RowCount = 1 };
        response.DimensionHeaders.Add(new DimensionHeader { Name = "eventName" });
        response.DimensionHeaders.Add(new DimensionHeader { Name = "dateHour" });
        response.DimensionHeaders.Add(new DimensionHeader { Name = "customUser:organization_id" });

        var row = new Row();
        row.DimensionValues.Add(new DimensionValue { Value = "login" });
        row.DimensionValues.Add(new DimensionValue { Value = "2026082510" });
        row.DimensionValues.Add(new DimensionValue { Value = "org1" });
        row.MetricValues.Add(new MetricValue { Value = "1" });
        response.Rows.Add(row);

        var dataSource = CreateDataSource(response);

        var result = await dataSource.GetRowsAsync(new AnalyticsDataQuery { PropertyId = "123456", Take = 10 });

        Assert.Equal("org1", Assert.Single(result.Events).Dimensions["organization_id"]);
    }

    [Fact]
    public async Task GetRowsAsync_TakeZero_ReturnsCountOnly()
    {
        var response = new RunReportResponse { RowCount = 42 };
        response.DimensionHeaders.Add(new DimensionHeader { Name = "eventName" });
        response.DimensionHeaders.Add(new DimensionHeader { Name = "dateHour" });

        var row = new Row();
        row.DimensionValues.Add(new DimensionValue { Value = "search" });
        row.DimensionValues.Add(new DimensionValue { Value = "2026082510" });
        row.MetricValues.Add(new MetricValue { Value = "1" });
        response.Rows.Add(row);

        var dataSource = CreateDataSource(response);

        var result = await dataSource.GetRowsAsync(new AnalyticsDataQuery { PropertyId = "123456", Take = 0 });

        Assert.Equal(1L, _capturedRequest.Limit);
        Assert.Equal(42, result.TotalCount);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task GetRowsAsync_CountSort_OmitsDateHourAndOrdersByMetric()
    {
        var dataSource = CreateDataSource();

        await dataSource.GetRowsAsync(new AnalyticsDataQuery
        {
            PropertyId = "123456",
            EventNames = new List<string> { ModuleConstants.EventNames.Search },
            DimensionNames = new List<string> { ModuleConstants.Dimensions.SearchTerm },
            SortBy = ModuleConstants.SortBy.Count,
            Take = 10,
        });

        Assert.Equal(ExpectedSearchCountDimensions, _capturedRequest.Dimensions.Select(x => x.Name));
        Assert.Equal("eventCount", Assert.Single(_capturedRequest.Metrics).Name);

        var orderBy = Assert.Single(_capturedRequest.OrderBys);
        Assert.True(orderBy.Desc);
        Assert.Null(orderBy.Dimension);
        Assert.Equal("eventCount", orderBy.Metric.MetricName);
    }

    [Fact]
    public async Task GetRowsAsync_ItemDimensions_BuildsItemReportRequest()
    {
        var dataSource = CreateDataSource();

        await dataSource.GetRowsAsync(new AnalyticsDataQuery
        {
            PropertyId = "123456",
            EventNames = new List<string> { ModuleConstants.EventNames.ViewItem },
            DimensionNames = new List<string> { ModuleConstants.Dimensions.ItemId, ModuleConstants.Dimensions.ItemName },
            DimensionFilters = new List<AnalyticsDimensionFilter>
            {
                new() { DimensionName = ModuleConstants.UserDimensions.OrganizationId, Values = new List<string> { "org1" } },
            },
            Take = 10,
        });

        Assert.Equal(ExpectedItemReportDimensions, _capturedRequest.Dimensions.Select(x => x.Name));
        Assert.Equal("itemsViewed", Assert.Single(_capturedRequest.Metrics).Name);

        var expressions = _capturedRequest.DimensionFilter.AndGroup.Expressions;
        Assert.Equal(2, expressions.Count);
        Assert.Equal("eventName", expressions[0].Filter.FieldName);
        Assert.Equal(ExpectedViewItemEventFilterValues, expressions[0].Filter.InListFilter.Values);
        Assert.Equal("customUser:organization_id", expressions[1].Filter.FieldName);

        var orderBy = Assert.Single(_capturedRequest.OrderBys);
        Assert.True(orderBy.Desc);
        Assert.Equal("dateHour", orderBy.Dimension.DimensionName);
    }

    [Fact]
    public async Task GetRowsAsync_ItemDimensionsCountSort_OmitsDateHourAndOrdersByItemsViewed()
    {
        var dataSource = CreateDataSource();

        await dataSource.GetRowsAsync(new AnalyticsDataQuery
        {
            PropertyId = "123456",
            EventNames = new List<string> { ModuleConstants.EventNames.ViewItem },
            DimensionNames = new List<string> { ModuleConstants.Dimensions.ItemId, ModuleConstants.Dimensions.ItemName },
            SortBy = ModuleConstants.SortBy.Count,
            Take = 10,
        });

        Assert.Equal(ExpectedItemCountDimensions, _capturedRequest.Dimensions.Select(x => x.Name));
        Assert.Equal("itemsViewed", Assert.Single(_capturedRequest.Metrics).Name);

        var orderBy = Assert.Single(_capturedRequest.OrderBys);
        Assert.True(orderBy.Desc);
        Assert.Equal("itemsViewed", orderBy.Metric.MetricName);
    }

    [Fact]
    public async Task GetRowsAsync_ItemRows_SingleEventName_FillsEventNameFromCriteria()
    {
        var response = new RunReportResponse { RowCount = 1 };
        response.DimensionHeaders.Add(new DimensionHeader { Name = "dateHour" });
        response.DimensionHeaders.Add(new DimensionHeader { Name = "itemId" });

        var row = new Row();
        row.DimensionValues.Add(new DimensionValue { Value = "2026082510" });
        row.DimensionValues.Add(new DimensionValue { Value = "SKU-001" });
        row.MetricValues.Add(new MetricValue { Value = "9" });
        response.Rows.Add(row);

        var dataSource = CreateDataSource(response);

        var result = await dataSource.GetRowsAsync(new AnalyticsDataQuery
        {
            PropertyId = "123456",
            EventNames = new List<string> { ModuleConstants.EventNames.ViewItem },
            DimensionNames = new List<string> { ModuleConstants.Dimensions.ItemId },
            Take = 10,
        });

        var analyticsEvent = Assert.Single(result.Events);
        Assert.Equal("view_item", analyticsEvent.EventName);
        Assert.Equal(new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc), analyticsEvent.OccurredAt);
        Assert.Equal(9, analyticsEvent.Count);
        Assert.Equal("SKU-001", analyticsEvent.Dimensions["itemId"]);
    }

    [Fact]
    public async Task GetRowsAsync_ItemRows_MultipleEventNames_LeavesEventNameNull()
    {
        var response = new RunReportResponse { RowCount = 1 };
        response.DimensionHeaders.Add(new DimensionHeader { Name = "itemId" });

        var row = new Row();
        row.DimensionValues.Add(new DimensionValue { Value = "SKU-001" });
        row.MetricValues.Add(new MetricValue { Value = "9" });
        response.Rows.Add(row);

        var dataSource = CreateDataSource(response);

        var result = await dataSource.GetRowsAsync(new AnalyticsDataQuery
        {
            PropertyId = "123456",
            EventNames = new List<string> { ModuleConstants.EventNames.ViewItem, ModuleConstants.EventNames.AddToCart },
            DimensionNames = new List<string> { ModuleConstants.Dimensions.ItemId },
            Take = 10,
        });

        Assert.Null(Assert.Single(result.Events).EventName);
    }

    private GoogleAnalyticsDataSource CreateDataSource(RunReportResponse response = null)
    {
        _reportClientMock
            .Setup(x => x.RunReportAsync(It.IsAny<RunReportRequest>()))
            .Callback((RunReportRequest request) => _capturedRequest = request)
            .ReturnsAsync(response ?? new RunReportResponse());

        return new GoogleAnalyticsDataSource(_reportClientMock.Object);
    }
}
