using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class AnalyticsService : IAnalyticsService
{
    private const int SummaryRowsLimit = 100_000;
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
            async settings =>
            {
                var query = CreateQuery(settings, criteria);
                query.Take = SummaryRowsLimit;

                var rows = await _dataSource.GetRowsAsync(query);
                return CreateSummaries(criteria, rows.Events);
            },
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
