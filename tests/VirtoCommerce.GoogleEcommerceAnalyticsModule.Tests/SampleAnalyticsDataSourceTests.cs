using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

public class SampleAnalyticsDataSourceTests
{
    private static readonly DateTime To = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly SampleAnalyticsDataSource _dataSource = new();

    [Fact]
    public async Task GetRowsAsync_SameQuery_ReturnsIdenticalData()
    {
        var first = await _dataSource.GetRowsAsync(CreateQuery());
        var second = await _dataSource.GetRowsAsync(CreateQuery());

        Assert.True(first.TotalCount > 0);
        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.Equal(first.Events.Select(Describe), second.Events.Select(Describe));
    }

    [Fact]
    public async Task GetRowsAsync_HonorsEventNames()
    {
        var query = CreateQuery();
        query.EventNames = new List<string> { ModuleConstants.EventNames.Login };

        var result = await _dataSource.GetRowsAsync(query);

        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, x => Assert.Equal(ModuleConstants.EventNames.Login, x.EventName));
    }

    [Fact]
    public async Task GetRowsAsync_HonorsDimensionFilterValues()
    {
        var query = CreateQuery();
        query.DimensionNames = new List<string> { ModuleConstants.UserDimensions.OrganizationId };
        query.DimensionFilters = new List<AnalyticsDimensionFilter>
        {
            new() { DimensionName = ModuleConstants.UserDimensions.OrganizationId, Values = new List<string> { "org1" } },
        };

        var result = await _dataSource.GetRowsAsync(query);

        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, x => Assert.Equal("org1", x.Dimensions[ModuleConstants.UserDimensions.OrganizationId]));
    }

    [Fact]
    public async Task GetRowsAsync_DifferentFilters_ProduceDifferentSeries()
    {
        var org1Query = CreateQuery();
        org1Query.DimensionFilters = new List<AnalyticsDimensionFilter>
        {
            new() { DimensionName = ModuleConstants.UserDimensions.OrganizationId, Values = new List<string> { "org1" } },
        };

        var org2Query = CreateQuery();
        org2Query.DimensionFilters = new List<AnalyticsDimensionFilter>
        {
            new() { DimensionName = ModuleConstants.UserDimensions.OrganizationId, Values = new List<string> { "org2" } },
        };

        var org1Result = await _dataSource.GetRowsAsync(org1Query);
        var org2Result = await _dataSource.GetRowsAsync(org2Query);

        Assert.NotEqual(org1Result.Events.Select(Describe), org2Result.Events.Select(Describe));
    }

    [Fact]
    public async Task GetRowsAsync_HonorsFromToBounds()
    {
        var query = CreateQuery();
        query.From = To.AddHours(-5);

        var result = await _dataSource.GetRowsAsync(query);

        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, x =>
        {
            Assert.True(x.OccurredAt >= query.From);
            Assert.True(x.OccurredAt <= To);
        });
    }

    [Fact]
    public async Task GetRowsAsync_PagingSlicesWithoutChangingTotalCount()
    {
        var fullResult = await _dataSource.GetRowsAsync(CreateQuery());

        var pageQuery = CreateQuery();
        pageQuery.Skip = 5;
        pageQuery.Take = 10;
        var pageResult = await _dataSource.GetRowsAsync(pageQuery);

        Assert.Equal(fullResult.TotalCount, pageResult.TotalCount);
        Assert.Equal(10, pageResult.Events.Count);
        Assert.Equal(fullResult.Events.Skip(5).Take(10).Select(Describe), pageResult.Events.Select(Describe));
    }

    [Fact]
    public async Task GetRowsAsync_TakeZero_ReturnsCountOnly()
    {
        var query = CreateQuery();
        query.Take = 0;

        var result = await _dataSource.GetRowsAsync(query);

        Assert.True(result.TotalCount > 0);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task GetRowsAsync_SortsByOccurredAtDescending()
    {
        var result = await _dataSource.GetRowsAsync(CreateQuery());

        var occurredAts = result.Events.Select(x => x.OccurredAt).ToList();
        Assert.Equal(occurredAts.OrderByDescending(x => x), occurredAts);
    }

    [Fact]
    public async Task GetRowsAsync_CountSort_AggregatesPerDimensionTuple()
    {
        var dateQuery = CreateQuery();
        dateQuery.DimensionNames = new List<string> { ModuleConstants.Dimensions.SearchTerm };

        var countQuery = CreateQuery();
        countQuery.DimensionNames = new List<string> { ModuleConstants.Dimensions.SearchTerm };
        countQuery.SortBy = ModuleConstants.SortBy.Count;

        var dateResult = await _dataSource.GetRowsAsync(dateQuery);
        var countResult = await _dataSource.GetRowsAsync(countQuery);

        Assert.NotEmpty(countResult.Events);
        Assert.True(countResult.TotalCount < dateResult.TotalCount);
        Assert.Equal(dateResult.Events.Sum(x => x.Count), countResult.Events.Sum(x => x.Count));
        Assert.All(countResult.Events, x => Assert.Null(x.OccurredAt));

        var tuples = countResult.Events.Select(x => $"{x.EventName}|{x.Dimensions[ModuleConstants.Dimensions.SearchTerm]}").ToList();
        Assert.Equal(tuples.Distinct().Count(), tuples.Count);

        var counts = countResult.Events.Select(x => x.Count).ToList();
        Assert.Equal(counts.OrderByDescending(x => x), counts);
    }

    [Fact]
    public async Task GetRowsAsync_CountSort_IsDeterministic()
    {
        var first = await _dataSource.GetRowsAsync(CreateCountQuery());
        var second = await _dataSource.GetRowsAsync(CreateCountQuery());

        Assert.True(first.TotalCount > 0);
        Assert.Equal(first.Events.Select(Describe), second.Events.Select(Describe));
    }

    [Fact]
    public async Task GetRowsAsync_CountSort_PagingSlicesWithoutChangingTotalCount()
    {
        var fullResult = await _dataSource.GetRowsAsync(CreateCountQuery());

        var pageQuery = CreateCountQuery();
        pageQuery.Skip = 2;
        pageQuery.Take = 3;
        var pageResult = await _dataSource.GetRowsAsync(pageQuery);

        Assert.Equal(fullResult.TotalCount, pageResult.TotalCount);
        Assert.Equal(fullResult.Events.Skip(2).Take(3).Select(Describe), pageResult.Events.Select(Describe));
    }

    private static AnalyticsDataQuery CreateCountQuery()
    {
        var query = CreateQuery();
        query.DimensionNames = new List<string> { ModuleConstants.Dimensions.SearchTerm };
        query.SortBy = ModuleConstants.SortBy.Count;
        return query;
    }

    private static AnalyticsDataQuery CreateQuery()
    {
        return new AnalyticsDataQuery
        {
            To = To,
            Take = 10_000,
        };
    }

    private static string Describe(AnalyticsEvent analyticsEvent)
    {
        var dimensions = string.Join(",", analyticsEvent.Dimensions
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value}"));

        return $"{analyticsEvent.EventName}|{analyticsEvent.OccurredAt:O}|{analyticsEvent.Count}|{dimensions}";
    }
}
