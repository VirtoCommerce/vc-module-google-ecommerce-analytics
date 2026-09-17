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
    // Count mode returns one row per event name, so a summary naming none is bounded by this — and so is the
    // probe fan-out below. A summary over hundreds of names is a reporting question, not a feature read.
    private const int MaxEventNames = 50;

    // Date mode orders by dateHour descending, so the newest bucket is the first row.
    private const int LatestBucketProbeSize = 1;

    // The probes are independent, so they run together — capped, because GA4 limits concurrent requests.
    private const int MaxProbeConcurrency = 4;
    // How the platform expresses Caching:CacheEnabled=false on the options it hands the factory.
    private static readonly TimeSpan CacheDisabled = TimeSpan.FromTicks(1);

    private const string SearchOperation = "events search";
    private const string SummariesOperation = "event summaries";
    private const string ConfigurationOperation = "configuration check";

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

    // `false` means exactly one thing: the settings resolved and carry no property id. A resolver that FAILED
    // has not answered the question, so it throws — an outage must not read as "not configured".
    public virtual async Task<bool> IsConfiguredAsync(string storeId)
    {
        AnalyticsDataApiSettings settings;

        try
        {
            settings = await _settingsResolver.ResolveAsync(storeId);
        }
        catch (Exception ex)
        {
            LogFailure(ConfigurationOperation, storeId, ex);
            throw CreateReadException(ConfigurationOperation, storeId);
        }

        return settings.IsConfigured;
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

    // StoreId is deliberately NOT required: an absent one resolves the global settings, which is the documented
    // store -> global -> default fallback and the shape a consumer uses for an unnarrowed read.
    protected virtual T PrepareCriteria<T>(T criteria)
        where T : AnalyticsEventCriteriaBase
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ValidateCriteria(criteria);

        return WithNormalizedDates(criteria);
    }

    protected virtual void ValidateCriteria(AnalyticsEventCriteriaBase criteria)
    {
        AnalyticsFilterBuilder.ValidateDimensionFilters(criteria.DimensionFilters, nameof(criteria));

        // Only a search carries extra dimension names; a summary can still reach the item scope through a filter.
        var dimensionNames = (criteria as AnalyticsEventSearchCriteria)?.DimensionNames;

        if (criteria.EventNames?.Count > 1 &&
            AnalyticsFilterBuilder.HasItemDimensions(dimensionNames, criteria.DimensionFilters))
        {
            throw new ArgumentException(
                "An item-scoped read cannot be narrowed to more than one event name: its rows carry no event name to tell them apart.",
                nameof(criteria));
        }
    }

    protected virtual async Task<T> GetOrCreateAsync<T>(
        string operation,
        AnalyticsEventCriteriaBase criteria,
        Func<AnalyticsDataApiSettings, Task<T>> factory)
        where T : class
    {
        // Arguments, then configuration, then the call — and only the call is cached, so fixing a criteria or a
        // setting takes effect on the next read instead of after the TTL.
        var settings = await ResolveSettingsAsync(operation, criteria.StoreId);

        T result;

        if (criteria.BypassCache)
        {
            result = await ReadAsync(operation, criteria.StoreId, factory, settings, cacheOptions: null);
        }
        else
        {
            // The property id is what the rows are OF: re-pointing a store must not keep serving the old numbers.
            var cacheKey = CacheKey.With(GetType(), operation, settings.PropertyId, criteria.GetCacheKey());

            result = await _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey,
                cacheOptions => ReadAsync(operation, criteria.StoreId, factory, settings, cacheOptions));
        }

        return result ?? throw CreateReadException(operation, criteria.StoreId);
    }

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
            // Not a state to answer with an empty result: a consumer handed "no data" would present it as fact.
            throw new AnalyticsException($"Google Analytics reporting is not configured{DescribeStore(storeId)}.");
        }

        return settings;
    }

    // A failed call is cached as a null: the entry stops a refused property spending the quota on every page
    // render, and every reader of it is still told the read failed.
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

    protected virtual AnalyticsException CreateReadException(string operation, string storeId)
    {
        return new AnalyticsException($"Google Analytics {operation} failed{DescribeStore(storeId)}.");
    }

    // An absent store id is a legitimate unnarrowed read, not a missing value, so it must not read as "store ''".
    private static string DescribeStore(string storeId)
    {
        return string.IsNullOrEmpty(storeId) ? string.Empty : $" for store '{storeId}'";
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

    // GA has no "max(dateHour)", so reducing a fetched series would transfer one row per event name PER HOUR to
    // produce two numbers. Two narrow reads answer it instead: 'count' mode for the totals, then a one-row 'date'
    // probe per name that has any.
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

        // A failed probe fails the whole read: a null LastOccurredAt reads as "never happened".
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
