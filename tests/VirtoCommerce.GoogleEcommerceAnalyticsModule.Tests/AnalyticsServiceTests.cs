using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Exceptions;
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
    private const string PropertyId = "123456";

    private static readonly DateTime To = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAnalyticsSettingsResolver> _settingsResolverMock = new();
    private readonly Mock<IGoogleAnalyticsReportClient> _reportClientMock = new();
    private readonly Mock<GoogleAnalyticsDataSource> _googleDataSourceMock;

    public AnalyticsServiceTests()
    {
        _googleDataSourceMock = new Mock<GoogleAnalyticsDataSource>(_reportClientMock.Object, NullLogger<GoogleAnalyticsDataSource>.Instance);
    }

    [Theory]
    [InlineData(PropertyId, true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public async Task IsConfiguredAsync_Matrix(string propertyId, bool expected)
    {
        var service = CreateService(new AnalyticsDataApiSettings { PropertyId = propertyId });

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

    // Step 1 of a Google call: the arguments, before anything loads configuration.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchEventsAsync_WithoutStoreId_ThrowsBeforeResolvingSettings(string storeId)
    {
        var service = CreateService();

        var criteria = CreateSearchCriteria();
        criteria.StoreId = storeId;

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchEventsAsync(criteria));
        _settingsResolverMock.Verify(x => x.ResolveAsync(It.IsAny<string>()), Times.Never);
    }

    // "Not configured" is a configuration error, not an answer: a consumer handed an empty result would present
    // "no activity" as fact.
    [Fact]
    public async Task SearchEventsAsync_NotConfigured_ThrowsWithoutQueryingSource()
    {
        var service = CreateService(new AnalyticsDataApiSettings());

        await Assert.ThrowsAsync<AnalyticsException>(() => service.SearchEventsAsync(CreateSearchCriteria()));

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Never);
    }

    // Only the Google call is cached. Configuration is re-read on every call, so the store configured a minute
    // after a failed read reports at once instead of after the TTL.
    [Fact]
    public async Task SearchEventsAsync_ConfiguredAfterAFailedRead_ReportsWithoutWaitingForTheTtl()
    {
        _settingsResolverMock
            .SetupSequence(x => x.ResolveAsync(StoreId))
            .ReturnsAsync(new AnalyticsDataApiSettings())
            .ReturnsAsync(new AnalyticsDataApiSettings { PropertyId = PropertyId });
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateService();

        await Assert.ThrowsAsync<AnalyticsException>(() => service.SearchEventsAsync(CreateSearchCriteria()));
        var result = await service.SearchEventsAsync(CreateSearchCriteria());

        Assert.Equal(1, result.TotalCount);
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
    public async Task SearchEventsAsync_DifferentDimensionFilterValues_UseDifferentCacheKeys()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        await service.SearchEventsAsync(CreateSearchCriteria(organizationId: "org1"));
        await service.SearchEventsAsync(CreateSearchCriteria(organizationId: "org2"));

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Exactly(2));
    }

    // The rows are OF a property: re-pointing the store must not keep serving the previous property's numbers.
    [Fact]
    public async Task SearchEventsAsync_PropertyIdChanged_DoesNotServeThePreviousPropertysRows()
    {
        _settingsResolverMock
            .SetupSequence(x => x.ResolveAsync(StoreId))
            .ReturnsAsync(new AnalyticsDataApiSettings { PropertyId = "111111" })
            .ReturnsAsync(new AnalyticsDataApiSettings { PropertyId = "222222" });
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateService();

        await service.SearchEventsAsync(CreateSearchCriteria());
        await service.SearchEventsAsync(CreateSearchCriteria());

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.Is<AnalyticsDataQuery>(q => q.PropertyId == "111111")), Times.Once);
        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.Is<AnalyticsDataQuery>(q => q.PropertyId == "222222")), Times.Once);
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
        Assert.Equal(PropertyId, capturedQuery.PropertyId);
        Assert.Equal(20, capturedQuery.Take);
        Assert.Equal(5, capturedQuery.Skip);
    }

    // The failed call is cached, not the empty result it used to return: the entry protects the quota, and every
    // reader of it is still told the read failed.
    [Fact]
    public async Task SearchEventsAsync_SourceFails_ThrowsAndCachesTheFailure()
    {
        _googleDataSourceMock
            .SetupSequence(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ThrowsAsync(new InvalidOperationException($"PermissionDenied on property {PropertyId}"))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        var failure = await Assert.ThrowsAsync<AnalyticsException>(() => service.SearchEventsAsync(CreateSearchCriteria()));
        await Assert.ThrowsAsync<AnalyticsException>(() => service.SearchEventsAsync(CreateSearchCriteria()));

        // What a consumer catches names the store and the operation; Google's own words stay in the log.
        Assert.Contains(StoreId, failure.Message);
        Assert.DoesNotContain(PropertyId, failure.Message);
        Assert.Null(failure.InnerException);
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

        await Assert.ThrowsAsync<AnalyticsException>(() => service.SearchEventsAsync(CreateSearchCriteria()));
        await Task.Delay(500, TestContext.Current.CancellationToken);
        var recovered = await service.SearchEventsAsync(CreateSearchCriteria());

        Assert.Equal(1, recovered.TotalCount);
        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Exactly(2));
    }

    // Left at the platform's sliding default, the entry evicts on the sliding clock and CacheTtlMinutes means
    // something other than what it says.
    [Fact]
    public async Task SearchEventsAsync_ShorterSlidingDefault_DoesNotEvictBeforeTheConfiguredTtl()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateService(
            new AnalyticsDataApiSettings { PropertyId = PropertyId, CacheTtlMinutes = 5 },
            slidingExpiration: TimeSpan.FromMilliseconds(50));

        await service.SearchEventsAsync(CreateSearchCriteria());
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await service.SearchEventsAsync(CreateSearchCriteria());

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Once);
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

    // Keeping the totals would answer "when did this last happen?" with a null that reads as "never".
    [Fact]
    public async Task GetEventSummariesAsync_OneProbeFails_ThrowsRatherThanReportNever()
    {
        var totals = CreateCountModeResult(("search", 3), ("login", 5));

        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.Is<AnalyticsDataQuery>(q => q.SortBy == ModuleConstants.SortBy.Count)))
            .ReturnsAsync(totals);
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.Is<AnalyticsDataQuery>(q =>
                q.SortBy != ModuleConstants.SortBy.Count && q.EventNames.Contains(ModuleConstants.EventNames.Login))))
            .ThrowsAsync(new InvalidOperationException("GA responded 429"));
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.Is<AnalyticsDataQuery>(q =>
                q.SortBy != ModuleConstants.SortBy.Count && q.EventNames.Contains(ModuleConstants.EventNames.Search))))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));

        var service = CreateGoogleConfiguredService();

        await Assert.ThrowsAsync<AnalyticsException>(() => service.GetEventSummariesAsync(
            CreateSummaryCriteria(ModuleConstants.EventNames.Search, ModuleConstants.EventNames.Login)));
    }

    [Fact]
    public async Task GetEventSummariesAsync_ReadsNarrowly_NeverTheWholeHourlySeries()
    {
        // Concurrent, because the probes run in parallel; enqueue order still puts the awaited totals read first.
        var queries = new ConcurrentQueue<AnalyticsDataQuery>();
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .Callback<AnalyticsDataQuery>(queries.Enqueue)
            .ReturnsAsync(CreateSearchResult(
                ("search", To.AddHours(-2), 2),
                ("search", To, 3),
                ("login", To.AddHours(-1), 5)));
        var service = CreateGoogleConfiguredService();

        var criteria = CreateSummaryCriteria(
            ModuleConstants.EventNames.Search,
            ModuleConstants.EventNames.Login,
            ModuleConstants.EventNames.SignUp);
        await service.GetEventSummariesAsync(criteria);

        // One totals read for all three names, then a newest-bucket probe only for the two that have events:
        // sign_up totals zero, so nothing is asked about its last occurrence.
        var reads = queries.ToList();
        Assert.Equal(3, reads.Count);

        var totals = reads[0];
        Assert.Equal(ModuleConstants.SortBy.Count, totals.SortBy);
        Assert.Equal(3, totals.Take);
        Assert.Equal(criteria.EventNames, totals.EventNames);

        var probes = reads.Skip(1).ToList();
        Assert.All(probes, x => Assert.Equal(ModuleConstants.SortBy.Date, x.SortBy));
        Assert.All(probes, x => Assert.Equal(1, x.Take));
        // Unordered: the probes run concurrently.
        Assert.Equal(
            [ModuleConstants.EventNames.Login, ModuleConstants.EventNames.Search],
            probes.Select(x => Assert.Single(x.EventNames)).OrderBy(x => x, StringComparer.Ordinal));

        // The point of the shape: no read is proportional to the number of hours in the range.
        Assert.All(reads, x => Assert.True(x.Take <= 3));
    }

    [Fact]
    public async Task GetEventSummariesAsync_NotConfigured_Throws()
    {
        var service = CreateService(new AnalyticsDataApiSettings());

        await Assert.ThrowsAsync<AnalyticsException>(() =>
            service.GetEventSummariesAsync(CreateSummaryCriteria(ModuleConstants.EventNames.Login)));

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Never);
    }

    [Fact]
    public async Task GetEventSummariesAsync_SourceFails_ThrowsAndCachesTheFailure()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ThrowsAsync(new InvalidOperationException("GA responded 400"));
        var service = CreateGoogleConfiguredService();

        await Assert.ThrowsAsync<AnalyticsException>(() =>
            service.GetEventSummariesAsync(CreateSummaryCriteria(ModuleConstants.EventNames.Login)));
        await Assert.ThrowsAsync<AnalyticsException>(() =>
            service.GetEventSummariesAsync(CreateSummaryCriteria(ModuleConstants.EventNames.Login)));

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Once);
    }

    private AnalyticsService CreateService(
        AnalyticsDataApiSettings settings = null,
        TimeSpan? failureCacheTtl = null,
        bool cacheEnabled = true,
        TimeSpan? slidingExpiration = null)
    {
        if (settings != null)
        {
            _settingsResolverMock
                .Setup(x => x.ResolveAsync(StoreId))
                .ReturnsAsync(settings);
        }

        var cachingOptions = new CachingOptions { CacheEnabled = cacheEnabled };
        if (slidingExpiration != null)
        {
            cachingOptions.CacheSlidingExpiration = slidingExpiration;
        }

        var memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var platformMemoryCache = new PlatformMemoryCache(memoryCache, Options.Create(cachingOptions), new Mock<ILogger<PlatformMemoryCache>>().Object);
        var logger = new Mock<ILogger<AnalyticsService>>().Object;

        return failureCacheTtl == null
            ? new AnalyticsService(_settingsResolverMock.Object, platformMemoryCache, _googleDataSourceMock.Object, logger)
            : new ShortFailureTtlAnalyticsService(failureCacheTtl.Value, _settingsResolverMock.Object, platformMemoryCache, _googleDataSourceMock.Object, logger);
    }

    private AnalyticsService CreateGoogleConfiguredService(TimeSpan? failureCacheTtl = null, bool cacheEnabled = true)
    {
        return CreateService(new AnalyticsDataApiSettings { PropertyId = PropertyId }, failureCacheTtl, cacheEnabled);
    }

    // The request only carries dates, so these are one Google query — and the key decides whether it runs twice.
    [Fact]
    public async Task SearchEventsAsync_CriteriaDifferingOnlyInTimeOfDay_UsesCache()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        var morning = CreateSearchCriteria();
        morning.To = To.Date.AddHours(9).AddMinutes(17);
        var evening = CreateSearchCriteria();
        evening.To = To.Date.AddHours(21).AddSeconds(4);

        await service.SearchEventsAsync(morning);
        await service.SearchEventsAsync(evening);

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Once);
    }

    [Fact]
    public async Task SearchEventsAsync_NormalizingDates_DoesNotMutateTheCallersCriteria()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        var criteria = CreateSearchCriteria();
        criteria.To = To.Date.AddHours(9).AddMinutes(17);

        await service.SearchEventsAsync(criteria);

        Assert.Equal(To.Date.AddHours(9).AddMinutes(17), criteria.To);
    }

    // Diagnostics must not be served a cached verdict.
    [Fact]
    public async Task SearchEventsAsync_BypassCache_ReadsEveryTime()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService();

        var criteria = CreateSearchCriteria();
        criteria.BypassCache = true;

        await service.SearchEventsAsync(criteria);
        await service.SearchEventsAsync(criteria);

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SearchEventsAsync_BypassCache_SourceFails_Throws()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ThrowsAsync(new InvalidOperationException("GA responded 400"));
        var service = CreateGoogleConfiguredService();

        var criteria = CreateSearchCriteria();
        criteria.BypassCache = true;

        await Assert.ThrowsAsync<AnalyticsException>(() => service.SearchEventsAsync(criteria));
    }

    // The platform expresses that switch as a one-tick TTL on the options it hands the factory.
    [Fact]
    public async Task SearchEventsAsync_PlatformCachingDisabled_ReadsEveryTime()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateGoogleConfiguredService(cacheEnabled: false);

        await service.SearchEventsAsync(CreateSearchCriteria());
        await service.SearchEventsAsync(CreateSearchCriteria());

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SearchEventsAsync_CacheTtlZero_ReadsEveryTime()
    {
        _googleDataSourceMock
            .Setup(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()))
            .ReturnsAsync(CreateSearchResult(("search", To, 3)));
        var service = CreateService(new AnalyticsDataApiSettings { PropertyId = PropertyId, CacheTtlMinutes = 0 });

        await service.SearchEventsAsync(CreateSearchCriteria());
        await service.SearchEventsAsync(CreateSearchCriteria());

        _googleDataSourceMock.Verify(x => x.GetRowsAsync(It.IsAny<AnalyticsDataQuery>()), Times.Exactly(2));
    }

    private static AnalyticsEventSearchCriteria CreateSearchCriteria(string organizationId = null)
    {
        var criteria = new AnalyticsEventSearchCriteria
        {
            StoreId = StoreId,
            EventNames = new List<string> { ModuleConstants.EventNames.Search },
            To = To,
            Take = 20,
        };

        if (organizationId != null)
        {
            criteria.DimensionFilters = new List<AnalyticsDimensionFilter>
            {
                new() { DimensionName = ModuleConstants.UserDimensions.OrganizationId, Values = new List<string> { organizationId } },
            };
        }

        return criteria;
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

    // Count mode carries no date; the probes are what fill it in.
    private static AnalyticsEventSearchResult CreateCountModeResult(params (string EventName, int Count)[] events)
    {
        return new AnalyticsEventSearchResult
        {
            TotalCount = events.Length,
            Events = events
                .Select(x => new AnalyticsEvent { EventName = x.EventName, Count = x.Count })
                .ToList(),
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
