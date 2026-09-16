using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Exceptions;
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
    // How the platform expresses Caching:CacheEnabled=false on the options it hands the factory.
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

    // A question, not a read: the caller is asking whether reading would work at all, so a store that cannot
    // report is the answer rather than a failure.
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
        criteria = PrepareCriteria(criteria);

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
            });

        return result.CloneTyped();
    }

    public virtual async Task<IList<AnalyticsEventSummary>> GetEventSummariesAsync(AnalyticsEventSummaryCriteria criteria)
    {
        criteria = PrepareCriteria(criteria);

        var result = await GetOrCreateAsync(
            SummariesOperation,
            criteria,
            settings => CreateSummariesAsync(settings, criteria));

        return result.Select(x => x.CloneTyped()).ToList();
    }

    // Step 1 of a Google call: the arguments. Nothing here loads configuration or reaches the cache, so a caller
    // that fixes its criteria is answered at once rather than after a TTL.
    protected virtual T PrepareCriteria<T>(T criteria)
        where T : AnalyticsEventCriteriaBase
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (string.IsNullOrWhiteSpace(criteria.StoreId))
        {
            throw new ArgumentException("A store id is required: it is what selects the GA4 property to report on.", nameof(criteria));
        }

        return WithNormalizedDates(criteria);
    }

    protected virtual async Task<T> GetOrCreateAsync<T>(
        string operation,
        AnalyticsEventCriteriaBase criteria,
        Func<AnalyticsDataApiSettings, Task<T>> factory)
        where T : class
    {
        var settings = await ResolveSettingsAsync(operation, criteria.StoreId);

        T result;

        if (criteria.BypassCache)
        {
            result = await ReadAsync(operation, criteria.StoreId, factory, settings, cacheOptions: null);
        }
        else
        {
            // The property id belongs in the key because it is what the rows are OF: re-pointing a store at
            // another property must not keep serving the previous property's numbers until the TTL runs out.
            var cacheKey = CacheKey.With(GetType(), operation, settings.PropertyId, criteria.GetCacheKey());

            result = await _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey,
                cacheOptions => ReadAsync(operation, criteria.StoreId, factory, settings, cacheOptions));
        }

        return result ?? throw CreateReadException(operation, criteria.StoreId);
    }

    // Step 2: the configuration. Cheap, calls nothing, and deliberately outside the cache — a cached "not
    // configured" would outlive the setting that fixed it.
    protected virtual async Task<AnalyticsDataApiSettings> ResolveSettingsAsync(string operation, string storeId)
    {
        AnalyticsDataApiSettings settings;

        try
        {
            settings = await _settingsResolver.ResolveAsync(storeId);
        }
        catch (Exception ex)
        {
            LogFailure(operation, storeId, ex);
            throw CreateReadException(operation, storeId);
        }

        if (!settings.IsConfigured)
        {
            // Not a state to answer with an empty result: a store with no property id cannot report at all, and
            // a consumer handed "no data" would present that as fact.
            throw new AnalyticsException($"Google Analytics reporting is not configured for store '{storeId}'.");
        }

        return settings;
    }

    // Step 3: the call, and the only step that is cached. A failed call is cached as a null — the entry is what
    // keeps a property Google refuses from spending the quota again on every page render — and every reader of
    // it is told the read failed rather than handed an empty result.
    protected virtual async Task<T> ReadAsync<T>(
        string operation,
        string storeId,
        Func<AnalyticsDataApiSettings, Task<T>> factory,
        AnalyticsDataApiSettings settings,
        MemoryCacheEntryOptions cacheOptions)
        where T : class
    {
        try
        {
            var result = await factory(settings);
            ApplyCacheTtl(cacheOptions, GetCacheTtl(settings));

            return result;
        }
        catch (Exception ex)
        {
            LogFailure(operation, storeId, ex);
            ApplyCacheTtl(cacheOptions, FailureCacheTtl);

            return null;
        }
    }

    // Google's own words never leave this module: what a consumer catches names the store and the operation,
    // because it cannot know how far its own error surface travels. The cause is in the log line above.
    protected virtual AnalyticsException CreateReadException(string operation, string storeId)
    {
        return new AnalyticsException($"Google Analytics {operation} failed for store '{storeId}'.");
    }

    // The request carries dates, so criteria differing only in time of day are one Google query — but
    // GetCacheKey() renders From/To at second precision and would miss the hit on a metered API.
    protected virtual T WithNormalizedDates<T>(T criteria)
        where T : AnalyticsEventCriteriaBase
    {
        if (IsMidnightOrAbsent(criteria.From) && IsMidnightOrAbsent(criteria.To))
        {
            return criteria;
        }

        var result = criteria.CloneTyped();
        result.From = criteria.From?.Date;
        result.To = criteria.To?.Date;

        return result;
    }

    private static bool IsMidnightOrAbsent(DateTime? value)
    {
        return value is null || value.Value.TimeOfDay == TimeSpan.Zero;
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

        // A failed probe fails the whole summary read: its null LastOccurredAt is indistinguishable from "this
        // event has never happened", so keeping the totals would hand a consumer a wrong answer as a fact.
        await Parallel.ForEachAsync(
            summaries.Where(x => x.TotalCount > 0),
            new ParallelOptions { MaxDegreeOfParallelism = MaxProbeConcurrency },
            async (summary, _) => summary.LastOccurredAt = await GetLastOccurredAtAsync(settings, criteria, summary.EventName));

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

    // The factory is handed the platform's DEFAULT options: a TTL written straight over them makes
    // CacheEnabled=false inert and leaves the 15-min sliding default to evict first.
    protected virtual void ApplyCacheTtl(MemoryCacheEntryOptions options, TimeSpan ttl)
    {
        if (options == null || options.AbsoluteExpirationRelativeToNow == CacheDisabled)
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
