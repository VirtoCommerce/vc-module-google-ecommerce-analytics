using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class AnalyticsService : IAnalyticsService
{
    // Count mode returns one row per event name, so the totals read is bounded by the names asked for — or, when
    // the caller names none, by this. GA4 allows a property up to 500 distinct event names, but a summary over
    // hundreds of them is a reporting question, not a feature read: this bounds the probe fan-out below with it.
    private const int MaxEventNames = 50;

    // Date mode orders by dateHour descending, so the newest bucket is the first row.
    private const int LatestBucketProbeSize = 1;

    // The probes are independent, so they run together instead of nose to tail. Capped rather than unbounded:
    // GA4 limits concurrent requests per property, and a criteria naming no events can carry MaxEventNames.
    private const int MaxProbeConcurrency = 4;
    // The platform expresses Caching:CacheEnabled=false as a one-tick TTL on the default entry options.
    private static readonly TimeSpan CacheDisabled = TimeSpan.FromTicks(1);

    private const string SearchOperation = "events search";
    private const string SummariesOperation = "event summaries";

    private readonly IAnalyticsSettingsResolver _settingsResolver;
    private readonly IPlatformMemoryCache _platformMemoryCache;
    private readonly IAnalyticsDataSource _dataSource;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IAnalyticsSettingsResolver settingsResolver,
        IPlatformMemoryCache platformMemoryCache,
        IAnalyticsDataSource dataSource,
        ILogger<AnalyticsService> logger)
    {
        _settingsResolver = settingsResolver;
        _platformMemoryCache = platformMemoryCache;
        _dataSource = dataSource;
        _logger = logger;
    }

    protected virtual TimeSpan FailureCacheTtl => TimeSpan.FromSeconds(60);

    public virtual async Task<bool> IsConfiguredAsync(string storeId)
    {
        try
        {
            var settings = await _settingsResolver.ResolveAsync(storeId);
            return settings.IsConfigured;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Google Analytics Data API settings for store {StoreId}", storeId);
            return false;
        }
    }

    public virtual async Task<AnalyticsEventSearchResult> SearchEventsAsync(AnalyticsEventSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        criteria = WithNormalizedDates(criteria);

        var result = await GetOrCreateAsync(
            SearchOperation,
            criteria,
            settings =>
            {
                var query = CreateQuery(settings, criteria);
                query.DimensionNames = criteria.DimensionNames;
                query.SortBy = criteria.SortBy;
                query.Take = criteria.Take;
                query.Skip = criteria.Skip;

                return _dataSource.GetRowsAsync(query);
            },
            AbstractTypeFactory<AnalyticsEventSearchResult>.TryCreateInstance);

        return result.CloneTyped();
    }

    public virtual async Task<IList<AnalyticsEventSummary>> GetEventSummariesAsync(AnalyticsEventSummaryCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        criteria = WithNormalizedDates(criteria);

        var result = await GetOrCreateAsync(
            SummariesOperation,
            criteria,
            settings => CreateSummariesAsync(settings, criteria),
            () => CreateEmptySummaries(criteria));

        return result.Select(x => x.CloneTyped()).ToList();
    }

    protected virtual async Task<T> GetOrCreateAsync<T>(
        string operation,
        AnalyticsEventCriteriaBase criteria,
        Func<AnalyticsDataApiSettings, Task<T>> factory,
        Func<T> createEmptyResult)
    {
        try
        {
            var settings = await _settingsResolver.ResolveAsync(criteria.StoreId);
            if (!settings.IsConfigured)
            {
                return createEmptyResult();
            }

            // Diagnostics asks for the live state, so it opts out rather than reading a cached verdict.
            if (criteria.BypassCache)
            {
                return await ReadAsync(operation, criteria, factory, createEmptyResult, settings, null);
            }

            var cacheKey = CacheKey.With(GetType(), operation, criteria.GetCacheKey());

            return await _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, cacheOptions =>
            {
                ApplyCacheTtl(cacheOptions, GetCacheTtl(settings));

                return ReadAsync(operation, criteria, factory, createEmptyResult, settings, cacheOptions);
            });
        }
        catch (Exception ex)
        {
            LogFailure(operation, criteria.StoreId, ex);
            return createEmptyResult();
        }
    }

    private async Task<T> ReadAsync<T>(
        string operation,
        AnalyticsEventCriteriaBase criteria,
        Func<AnalyticsDataApiSettings, Task<T>> factory,
        Func<T> createEmptyResult,
        AnalyticsDataApiSettings settings,
        MemoryCacheEntryOptions cacheOptions)
    {
        try
        {
            return await factory(settings);
        }
        catch (Exception ex)
        {
            LogFailure(operation, criteria.StoreId, ex);

            if (cacheOptions != null)
            {
                ApplyCacheTtl(cacheOptions, FailureCacheTtl);
            }

            return createEmptyResult();
        }
    }

    // The request carries dates, so two criteria differing only in time of day are the same Google query —
    // while GetCacheKey() renders From/To at second precision and would miss the hit on a metered API.
    protected virtual T WithNormalizedDates<T>(T criteria)
        where T : AnalyticsEventCriteriaBase
    {
        if (criteria.From?.TimeOfDay == TimeSpan.Zero && criteria.To?.TimeOfDay == TimeSpan.Zero)
        {
            return criteria;
        }

        var result = criteria.CloneTyped();
        result.From = criteria.From?.Date;
        result.To = criteria.To?.Date;

        return result;
    }

    // A summary is a sum and a newest-occurrence per event name, and GA has no "max(dateHour)" aggregation — so
    // reducing a fetched series here would transfer one row per event name PER HOUR (years of rows) to produce two
    // numbers. Two narrow reads answer it instead: 'count' mode collapses to one row per event name carrying the
    // summed metric, and a one-row 'date' probe per event name carries its newest bucket. Names with no events at
    // all are not probed.
    protected virtual async Task<IList<AnalyticsEventSummary>> CreateSummariesAsync(
        AnalyticsDataApiSettings settings,
        AnalyticsEventSummaryCriteria criteria)
    {
        var totalsQuery = CreateQuery(settings, criteria);
        totalsQuery.SortBy = ModuleConstants.SortBy.Count;
        totalsQuery.Take = criteria.EventNames.IsNullOrEmpty() ? MaxEventNames : criteria.EventNames.Count;

        var totals = await _dataSource.GetRowsAsync(totalsQuery);

        // Count-mode rows carry no date, so the summaries come back with a null LastOccurredAt that the probe fills.
        var summaries = CreateSummaries(criteria, totals.Events);

        await Parallel.ForEachAsync(
            summaries.Where(x => x.TotalCount > 0),
            new ParallelOptions { MaxDegreeOfParallelism = MaxProbeConcurrency },
            async (summary, _) =>
            {
                try
                {
                    summary.LastOccurredAt = await GetLastOccurredAtAsync(settings, criteria, summary.EventName);
                }
                catch (Exception ex)
                {
                    // The totals read already succeeded. Faulting the loop would discard every count and cache
                    // the zeros, so a failed probe costs its own last-occurrence and nothing else.
                    LogFailure($"last occurrence of '{summary.EventName}'", criteria.StoreId, ex);
                }
            });

        return summaries;
    }

    protected virtual async Task<DateTime?> GetLastOccurredAtAsync(
        AnalyticsDataApiSettings settings,
        AnalyticsEventSummaryCriteria criteria,
        string eventName)
    {
        var query = CreateQuery(settings, criteria);
        query.EventNames = [eventName];
        query.SortBy = ModuleConstants.SortBy.Date;
        query.Take = LatestBucketProbeSize;

        var rows = await _dataSource.GetRowsAsync(query);

        return rows.Events.Where(x => x.EventName.EqualsIgnoreCase(eventName)).Max(x => x.OccurredAt);
    }

    protected virtual AnalyticsDataQuery CreateQuery(AnalyticsDataApiSettings settings, AnalyticsEventCriteriaBase criteria)
    {
        var query = AbstractTypeFactory<AnalyticsDataQuery>.TryCreateInstance();

        query.PropertyId = settings.PropertyId;
        query.EventNames = criteria.EventNames;
        query.DimensionFilters = criteria.DimensionFilters;
        query.From = criteria.From;
        query.To = criteria.To;

        return query;
    }

    // Requested event names still yield zero-count summaries. A criteria naming NO names has nothing to shape
    // them from, so that one does come back empty.
    protected virtual IList<AnalyticsEventSummary> CreateEmptySummaries(AnalyticsEventSummaryCriteria criteria)
    {
        return CreateSummaries(criteria, []);
    }

    protected virtual IList<AnalyticsEventSummary> CreateSummaries(AnalyticsEventSummaryCriteria criteria, IList<AnalyticsEvent> events)
    {
        var aggregates = new Dictionary<string, (int TotalCount, DateTime? LastOccurredAt)>(StringComparer.OrdinalIgnoreCase);

        foreach (var analyticsEvent in events.Where(x => !string.IsNullOrEmpty(x.EventName)))
        {
            aggregates.TryGetValue(analyticsEvent.EventName, out var aggregate);

            aggregates[analyticsEvent.EventName] = (
                aggregate.TotalCount + analyticsEvent.Count,
                aggregate.LastOccurredAt == null || analyticsEvent.OccurredAt > aggregate.LastOccurredAt
                    ? analyticsEvent.OccurredAt
                    : aggregate.LastOccurredAt);
        }

        var eventNames = criteria.EventNames.IsNullOrEmpty() ? aggregates.Keys.ToList() : criteria.EventNames;

        return eventNames
            .Select(eventName =>
            {
                var summary = AbstractTypeFactory<AnalyticsEventSummary>.TryCreateInstance();
                summary.EventName = eventName;

                if (aggregates.TryGetValue(eventName, out var aggregate))
                {
                    summary.TotalCount = aggregate.TotalCount;
                    summary.LastOccurredAt = aggregate.LastOccurredAt;
                }

                return summary;
            })
            .ToList();
    }

    protected virtual TimeSpan GetCacheTtl(AnalyticsDataApiSettings settings)
    {
        return TimeSpan.FromMinutes(settings.CacheTtlMinutes);
    }

    // Mirrors SalesRep's StatisticsCache.Apply: the platform hands the factory its DEFAULT entry options, so a
    // TTL written straight over them makes Caching:CacheEnabled=false inert and leaves the platform's sliding
    // default (15 min) to evict before the TTL this module documents as a setting.
    protected virtual void ApplyCacheTtl(MemoryCacheEntryOptions options, TimeSpan ttl)
    {
        if (options.AbsoluteExpirationRelativeToNow == CacheDisabled)
        {
            return;
        }

        options.SlidingExpiration = null;
        options.AbsoluteExpirationRelativeToNow = ttl > TimeSpan.Zero ? ttl : CacheDisabled;
    }

    private void LogFailure(string operation, string storeId, Exception exception)
    {
        _logger.LogWarning(exception, "Google Analytics {Operation} failed for store {StoreId}", operation, storeId);
    }
}
