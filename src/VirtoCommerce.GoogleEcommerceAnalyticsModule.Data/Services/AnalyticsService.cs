using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    // the caller names none, by GA4's per-property cap on distinct event names.
    private const int MaxEventNames = 500;

    // Date mode orders by dateHour descending, so the newest bucket is the first row.
    private const int LatestBucketProbeSize = 1;
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

            var cacheKey = CacheKey.With(GetType(), operation, criteria.GetCacheKey());

            return await _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async cacheOptions =>
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = GetCacheTtl(settings);

                try
                {
                    return await factory(settings);
                }
                catch (Exception ex)
                {
                    LogFailure(operation, criteria.StoreId, ex);
                    cacheOptions.AbsoluteExpirationRelativeToNow = FailureCacheTtl;
                    return createEmptyResult();
                }
            });
        }
        catch (Exception ex)
        {
            LogFailure(operation, criteria.StoreId, ex);
            return createEmptyResult();
        }
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

        foreach (var summary in summaries.Where(x => x.TotalCount > 0))
        {
            summary.LastOccurredAt = await GetLastOccurredAtAsync(settings, criteria, summary.EventName);
        }

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

        return rows.Events.Where(x => x.EventName == eventName).Max(x => x.OccurredAt);
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

    // Not an empty list: requested event names still yield zero-count summaries.
    protected virtual IList<AnalyticsEventSummary> CreateEmptySummaries(AnalyticsEventSummaryCriteria criteria)
    {
        return CreateSummaries(criteria, []);
    }

    protected virtual IList<AnalyticsEventSummary> CreateSummaries(AnalyticsEventSummaryCriteria criteria, IList<AnalyticsEvent> events)
    {
        var aggregates = new Dictionary<string, (int TotalCount, DateTime? LastOccurredAt)>();

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
        return TimeSpan.FromMinutes(Math.Max(1, settings.CacheTtlMinutes));
    }

    private void LogFailure(string operation, string storeId, Exception exception)
    {
        _logger.LogWarning(exception, "Google Analytics {Operation} failed for store {StoreId}", operation, storeId);
    }
}
