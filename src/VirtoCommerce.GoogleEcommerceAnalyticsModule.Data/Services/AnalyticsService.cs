using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class AnalyticsService : IAnalyticsService
{
    private const int SummaryRowsLimit = 100_000;

    private readonly IAnalyticsSettingsResolver _settingsResolver;
    private readonly IPlatformMemoryCache _platformMemoryCache;
    private readonly GoogleAnalyticsDataSource _googleAnalyticsDataSource;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IAnalyticsSettingsResolver settingsResolver,
        IPlatformMemoryCache platformMemoryCache,
        GoogleAnalyticsDataSource googleAnalyticsDataSource,
        ILogger<AnalyticsService> logger)
    {
        _settingsResolver = settingsResolver;
        _platformMemoryCache = platformMemoryCache;
        _googleAnalyticsDataSource = googleAnalyticsDataSource;
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

        try
        {
            var settings = await _settingsResolver.ResolveAsync(criteria.StoreId);
            var dataSource = ResolveDataSource(settings);
            if (dataSource == null)
            {
                return AbstractTypeFactory<AnalyticsEventSearchResult>.TryCreateInstance();
            }

            var cacheKey = CacheKey.With(GetType(), nameof(SearchEventsAsync), GetCriteriaCacheKey(criteria));
            var result = await _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async cacheOptions =>
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = GetCacheTtl(settings);

                try
                {
                    var query = CreateQuery(settings, criteria);
                    query.DimensionNames = criteria.DimensionNames;
                    query.SortBy = criteria.SortBy;
                    query.Take = criteria.Take;
                    query.Skip = criteria.Skip;

                    return await dataSource.GetRowsAsync(query);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Google Analytics events search failed for store {StoreId}", criteria.StoreId);
                    cacheOptions.AbsoluteExpirationRelativeToNow = FailureCacheTtl;
                    return AbstractTypeFactory<AnalyticsEventSearchResult>.TryCreateInstance();
                }
            });

            return result?.CloneTyped() ?? AbstractTypeFactory<AnalyticsEventSearchResult>.TryCreateInstance();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Analytics events search failed for store {StoreId}", criteria.StoreId);
            return AbstractTypeFactory<AnalyticsEventSearchResult>.TryCreateInstance();
        }
    }

    public virtual async Task<IList<AnalyticsEventSummary>> GetEventSummariesAsync(AnalyticsEventSummaryCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        try
        {
            var settings = await _settingsResolver.ResolveAsync(criteria.StoreId);
            var dataSource = ResolveDataSource(settings);
            if (dataSource == null)
            {
                return CreateSummaries(criteria, new List<AnalyticsEvent>());
            }

            var cacheKey = CacheKey.With(GetType(), nameof(GetEventSummariesAsync), GetCriteriaCacheKey(criteria));
            var result = await _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async cacheOptions =>
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = GetCacheTtl(settings);

                try
                {
                    var query = CreateQuery(settings, criteria);
                    query.Take = SummaryRowsLimit;

                    var rows = await dataSource.GetRowsAsync(query);
                    return CreateSummaries(criteria, rows.Events);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Google Analytics event summaries failed for store {StoreId}", criteria.StoreId);
                    cacheOptions.AbsoluteExpirationRelativeToNow = FailureCacheTtl;
                    return CreateSummaries(criteria, new List<AnalyticsEvent>());
                }
            });

            return result?.Select(x => x.CloneTyped()).ToList() ?? CreateSummaries(criteria, new List<AnalyticsEvent>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Analytics event summaries failed for store {StoreId}", criteria.StoreId);
            return CreateSummaries(criteria, new List<AnalyticsEvent>());
        }
    }

    protected virtual IAnalyticsDataSource ResolveDataSource(AnalyticsDataApiSettings settings)
    {
        return settings.IsConfigured ? _googleAnalyticsDataSource : null;
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
        var groups = events
            .Where(x => !string.IsNullOrEmpty(x.EventName))
            .GroupBy(x => x.EventName)
            .ToDictionary(x => x.Key, x => x.ToList());

        var eventNames = criteria.EventNames?.Count > 0 ? criteria.EventNames : groups.Keys.ToList();

        return eventNames
            .Select(eventName =>
            {
                var summary = AbstractTypeFactory<AnalyticsEventSummary>.TryCreateInstance();
                summary.EventName = eventName;

                if (groups.TryGetValue(eventName, out var group))
                {
                    summary.TotalCount = group.Sum(x => x.Count);
                    summary.LastOccurredAt = group.Max(x => x.OccurredAt);
                }

                return summary;
            })
            .ToList();
    }

    protected virtual string GetCriteriaCacheKey(AnalyticsEventCriteriaBase criteria)
    {
        return JsonConvert.SerializeObject(criteria);
    }

    private static TimeSpan GetCacheTtl(AnalyticsDataApiSettings settings)
    {
        return TimeSpan.FromMinutes(Math.Max(1, settings.CacheTtlMinutes));
    }
}
