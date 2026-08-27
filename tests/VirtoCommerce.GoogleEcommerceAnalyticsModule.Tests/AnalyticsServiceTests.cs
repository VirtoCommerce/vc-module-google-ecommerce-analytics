using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

public class AnalyticsServiceTests
{
    private const string StoreId = "test-store";

    private static readonly DateTime To = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAnalyticsSettingsResolver> _settingsResolverMock = new();
    private readonly Mock<IGoogleAnalyticsReportClient> _reportClientMock = new();
    private readonly Mock<GoogleAnalyticsDataSource> _googleDataSourceMock;

    public AnalyticsServiceTests()
    {
        _googleDataSourceMock = new Mock<GoogleAnalyticsDataSource>(_reportClientMock.Object);
    }

    [Theory]
    [InlineData("123456", null, true)]
    [InlineData("123456", "{}", true)]
    [InlineData(null, "{}", false)]
    [InlineData(null, null, false)]
    public async Task IsConfiguredAsync_Matrix(string propertyId, string credentialJson, bool expected)
    {
        var service = CreateService(new AnalyticsDataApiSettings
        {
            PropertyId = propertyId,
            CredentialJson = credentialJson,
        });

        Assert.Equal(expected, await service.IsConfiguredAsync(StoreId));
    }

    [Fact]
    public async Task IsConfiguredAsync_ResolverThrows_ReturnsFalse()
    {
        _settingsResolverMock
            .Setup(x => x.ResolveAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var service = CreateService();

        Assert.False(await service.IsConfiguredAsync(StoreId));
    }

    [Fact]
    public async Task SearchEventsAsync_NotConfigured_ReturnsEmptyWithoutQueryingSource()
    {
        var service = CreateService(new AnalyticsDataApiSettings());

        var result = await service.SearchEventsAsync(CreateSearchCriteria());

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Events);
        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Never);
    }

    [Fact]
    public async Task SearchEventsAsync_SameCriteria_UsesCache()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        var first = await service.SearchEventsAsync(CreateSearchCriteria());
        var second = await service.SearchEventsAsync(CreateSearchCriteria());

        Assert.Equal(1, first.TotalCount);
        Assert.Equal(1, second.TotalCount);
        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Once);
    }

    [Fact]
    public async Task SearchEventsAsync_DifferentCriteria_QueriesSourceAgain()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        await service.SearchEventsAsync(CreateSearchCriteria());
        var otherCriteria = CreateSearchCriteria();
        otherCriteria.SortBy = ModuleConstants.SortBy.Count;
        await service.SearchEventsAsync(otherCriteria);

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SearchEventsAsync_ReturnsClonedResult()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        var first = await service.SearchEventsAsync(CreateSearchCriteria());
        var second = await service.SearchEventsAsync(CreateSearchCriteria());

        Assert.NotSame(first, second);
        Assert.NotSame(first.Events[0], second.Events[0]);
    }

    [Fact]
    public async Task SearchEventsAsync_PropagatesCriteriaToQuery()
    {
        AnalyticsDataQuery capturedQuery = null;
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .Callback((AnalyticsDataQuery query) => capturedQuery = query)
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        var criteria = CreateSearchCriteria();
        criteria.SortBy = ModuleConstants.SortBy.Count;
        criteria.DimensionNames = new List<string> { ModuleConstants.Dimensions.SearchTerm };
        criteria.Skip = 5;
        await service.SearchEventsAsync(criteria);

        Assert.Equal(ModuleConstants.SortBy.Count, capturedQuery.SortBy);
        Assert.Equal(criteria.DimensionNames, capturedQuery.DimensionNames);
        Assert.Equal("123456", capturedQuery.PropertyId);
        Assert.Equal("{}", capturedQuery.CredentialJson);
        Assert.Equal(20, capturedQuery.Take);
        Assert.Equal(5, capturedQuery.Skip);
    }

    [Fact]
    public async Task SearchEventsAsync_SourceFails_CachesEmptyResultForFailureTtl()
    {
        _googleDataSourceMock
            .SetupSequence(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ThrowsAsync(new InvalidOperationException("GA responded 400"))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        var failed = await service.SearchEventsAsync(CreateSearchCriteria());
        var cached = await service.SearchEventsAsync(CreateSearchCriteria());

        Assert.Equal(0, failed.TotalCount);
        Assert.Empty(failed.Events);
        Assert.Equal(0, cached.TotalCount);
        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Once);
    }

    [Fact]
    public async Task SearchEventsAsync_FailureCacheExpires_RecoversQuickly()
    {
        _googleDataSourceMock
            .SetupSequence(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ThrowsAsync(new InvalidOperationException("GA responded 400"))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService(failureCacheTtl: TimeSpan.FromMilliseconds(100));

        var failed = await service.SearchEventsAsync(CreateSearchCriteria());
        await Task.Delay(500, TestContext.Current.CancellationToken);
        var recovered = await service.SearchEventsAsync(CreateSearchCriteria());

        Assert.Equal(0, failed.TotalCount);
        Assert.Equal(1, recovered.TotalCount);
        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetEventSummariesAsync_AggregatesPerRequestedEventName()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(
                ("search", To.AddHours(-2), 2),
                ("search", To, 3),
                ("login", To.AddHours(-1), 5)));
        var service = CreateGoogleConfiguredService();

        var criteria = CreateSummaryCriteria(
            ModuleConstants.EventNames.Search,
            ModuleConstants.EventNames.Login,
            ModuleConstants.EventNames.SignUp);
        var summaries = await service.GetEventSummariesAsync(criteria);

        Assert.Equal(3, summaries.Count);

        var search = summaries.First(x => x.EventName == ModuleConstants.EventNames.Search);
        Assert.Equal(5, search.TotalCount);
        Assert.Equal(To, search.LastOccurredAt);

        var login = summaries.First(x => x.EventName == ModuleConstants.EventNames.Login);
        Assert.Equal(5, login.TotalCount);
        Assert.Equal(To.AddHours(-1), login.LastOccurredAt);

        var signUp = summaries.First(x => x.EventName == ModuleConstants.EventNames.SignUp);
        Assert.Equal(0, signUp.TotalCount);
        Assert.Null(signUp.LastOccurredAt);
    }

    [Fact]
    public async Task GetEventSummariesAsync_NotConfigured_ReturnsZeroSummariesPerRequestedName()
    {
        var service = CreateService(new AnalyticsDataApiSettings());

        var summaries = await service.GetEventSummariesAsync(CreateSummaryCriteria(ModuleConstants.EventNames.Login));

        var summary = Assert.Single(summaries);
        Assert.Equal(ModuleConstants.EventNames.Login, summary.EventName);
        Assert.Equal(0, summary.TotalCount);
        Assert.Null(summary.LastOccurredAt);
    }

    [Fact]
    public async Task GetEventSummariesAsync_SourceFails_CachesZeroSummariesForFailureTtl()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ThrowsAsync(new InvalidOperationException("GA responded 400"));
        var service = CreateGoogleConfiguredService();

        var failed = await service.GetEventSummariesAsync(CreateSummaryCriteria(ModuleConstants.EventNames.Login));
        var cached = await service.GetEventSummariesAsync(CreateSummaryCriteria(ModuleConstants.EventNames.Login));

        Assert.Equal(0, Assert.Single(failed).TotalCount);
        Assert.Equal(0, Assert.Single(cached).TotalCount);
        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Once);
    }

    private AnalyticsService CreateService(AnalyticsDataApiSettings settings = null, TimeSpan? failureCacheTtl = null)
    {
        if (settings != null)
        {
            _settingsResolverMock
                .Setup(x => x.ResolveAsync(StoreId))
                .ReturnsAsync(settings);
        }

        var memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var platformMemoryCache = new PlatformMemoryCache(memoryCache, Options.Create(new CachingOptions()), new Mock<ILogger<PlatformMemoryCache>>().Object);
        var logger = new Mock<ILogger<AnalyticsService>>().Object;

        return failureCacheTtl == null
            ? new AnalyticsService(_settingsResolverMock.Object, platformMemoryCache, _googleDataSourceMock.Object, logger)
            : new ShortFailureTtlAnalyticsService(failureCacheTtl.Value, _settingsResolverMock.Object, platformMemoryCache, _googleDataSourceMock.Object, logger);
    }

    private AnalyticsService CreateGoogleConfiguredService(TimeSpan? failureCacheTtl = null)
    {
        return CreateService(new AnalyticsDataApiSettings { PropertyId = "123456", CredentialJson = "{}" }, failureCacheTtl);
    }

    private static AnalyticsEventSearchCriteria CreateSearchCriteria()
    {
        return new AnalyticsEventSearchCriteria
        {
            StoreId = StoreId,
            EventNames = new List<string> { ModuleConstants.EventNames.Search },
            To = To,
            Take = 20,
        };
    }

    private static AnalyticsEventSummaryCriteria CreateSummaryCriteria(params string[] eventNames)
    {
        return new AnalyticsEventSummaryCriteria
        {
            StoreId = StoreId,
            EventNames = eventNames.ToList(),
            To = To,
        };
    }

    private static AnalyticsEventSearchResult CreateSearchResult(params (string EventName, DateTime OccurredAt, int Count)[] events)
    {
        return new AnalyticsEventSearchResult
        {
            TotalCount = events.Length,
            Events = events
                .Select(x => new AnalyticsEvent { EventName = x.EventName, OccurredAt = x.OccurredAt, Count = x.Count })
                .ToList(),
        };
    }

    private sealed class ShortFailureTtlAnalyticsService : AnalyticsService
    {
        private readonly TimeSpan _failureCacheTtl;

        public ShortFailureTtlAnalyticsService(
            TimeSpan failureCacheTtl,
            IAnalyticsSettingsResolver settingsResolver,
            IPlatformMemoryCache platformMemoryCache,
            GoogleAnalyticsDataSource googleAnalyticsDataSource,
            ILogger<AnalyticsService> logger)
            : base(settingsResolver, platformMemoryCache, googleAnalyticsDataSource, logger)
        {
            _failureCacheTtl = failureCacheTtl;
        }

        protected override TimeSpan FailureCacheTtl => _failureCacheTtl;
    }
}
