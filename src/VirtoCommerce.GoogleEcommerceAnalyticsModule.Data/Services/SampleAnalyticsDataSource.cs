using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class SampleAnalyticsDataSource : IAnalyticsDataSource
{
    private const int MaxHourBuckets = 24 * 14;
    private const int BucketDensityPercent = 30;
    private const int PercentScale = 100;
    private const int MaxEventCount = 5;
    private const int GeneratedDimensionValueVariants = 5;
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;

    private static readonly string[] DefaultEventNames =
    {
        ModuleConstants.EventNames.Search,
        ModuleConstants.EventNames.ViewSearchResults,
        ModuleConstants.EventNames.ViewItem,
        ModuleConstants.EventNames.Login,
        ModuleConstants.EventNames.SignUp,
        ModuleConstants.EventNames.AddToCart,
        ModuleConstants.EventNames.Purchase,
    };

    private static readonly string[] SampleSearchTerms = { "drill", "hammer", "socket wrench", "paint", "ladder", "hex bolt" };
    private static readonly string[] SampleItemCodes = { "SAMPLE-0001", "SAMPLE-0002", "SAMPLE-0003", "SAMPLE-0004", "SAMPLE-0005", "SAMPLE-0006" };
    private static readonly string[] SampleItemNames = { "Cordless Drill 18V", "Claw Hammer", "Socket Wrench Set", "Acrylic Paint 5L", "Aluminum Ladder", "Hex Bolt M8" };
    private static readonly string[] SampleItemListNames = { "Search Results", "Related Products", "Category" };

    public virtual Task<AnalyticsEventSearchResult> GetRowsAsync(AnalyticsDataQuery query)
    {
        var result = AbstractTypeFactory<AnalyticsEventSearchResult>.TryCreateInstance();

        var to = TruncateToHour(query.To ?? DateTime.UtcNow);
        var from = query.From ?? to.AddHours(-(MaxHourBuckets - 1));
        var eventNames = query.EventNames?.Count > 0 ? query.EventNames : DefaultEventNames;
        var filterSignature = GetFilterSignature(query);

        var events = GenerateEvents(query, from, to, eventNames, filterSignature);

        if (ModuleConstants.SortBy.Count.EqualsIgnoreCase(query.SortBy))
        {
            events = AggregateByDimensionTuple(events);
        }

        result.TotalCount = events.Count;
        result.Events = query.Take > 0 ? events.Skip(query.Skip).Take(query.Take).ToList() : new List<AnalyticsEvent>();

        return Task.FromResult(result);
    }

    protected virtual IList<AnalyticsEvent> GenerateEvents(AnalyticsDataQuery query, DateTime from, DateTime to, IList<string> eventNames, string filterSignature)
    {
        var events = new List<AnalyticsEvent>();

        for (var bucketIndex = 0; bucketIndex < MaxHourBuckets; bucketIndex++)
        {
            var bucket = to.AddHours(-bucketIndex);
            if (bucket < from)
            {
                break;
            }

            foreach (var eventName in eventNames)
            {
                var seed = GetStableHash(eventName, bucketIndex.ToString(CultureInfo.InvariantCulture), filterSignature);
                if (seed % PercentScale < BucketDensityPercent)
                {
                    events.Add(CreateEvent(query, eventName, bucket, seed));
                }
            }
        }

        return events;
    }

    protected virtual AnalyticsEvent CreateEvent(AnalyticsDataQuery query, string eventName, DateTime bucket, int seed)
    {
        var analyticsEvent = AbstractTypeFactory<AnalyticsEvent>.TryCreateInstance();
        analyticsEvent.EventName = eventName;
        analyticsEvent.OccurredAt = bucket;
        analyticsEvent.Count = 1 + seed / PercentScale % MaxEventCount;

        foreach (var dimensionName in query.DimensionNames ?? Array.Empty<string>())
        {
            var value = GetDimensionValue(dimensionName, query.DimensionFilters, seed);
            if (value != null)
            {
                analyticsEvent.Dimensions[dimensionName] = value;
            }
        }

        return analyticsEvent;
    }

    protected virtual IList<AnalyticsEvent> AggregateByDimensionTuple(IList<AnalyticsEvent> events)
    {
        return events
            .GroupBy(GetDimensionTupleKey)
            .Select(group =>
            {
                var first = group.First();

                var analyticsEvent = AbstractTypeFactory<AnalyticsEvent>.TryCreateInstance();
                analyticsEvent.EventName = first.EventName;
                analyticsEvent.Count = group.Sum(x => x.Count);

                foreach (var dimension in first.Dimensions)
                {
                    analyticsEvent.Dimensions[dimension.Key] = dimension.Value;
                }

                return analyticsEvent;
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(GetDimensionTupleKey, StringComparer.Ordinal)
            .ToList();
    }

    protected virtual string GetDimensionTupleKey(AnalyticsEvent analyticsEvent)
    {
        var dimensions = string.Join(",", analyticsEvent.Dimensions
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value}"));

        return $"{analyticsEvent.EventName}|{dimensions}";
    }

    protected virtual string GetDimensionValue(string dimensionName, IList<AnalyticsDimensionFilter> filters, int seed)
    {
        var filter = filters?.FirstOrDefault(x => x.DimensionName == dimensionName && x.Values?.Count > 0);
        if (filter != null)
        {
            return filter.Values[seed % filter.Values.Count];
        }

        return GetGeneratedDimensionValue(dimensionName, seed);
    }

    protected virtual string GetGeneratedDimensionValue(string dimensionName, int seed)
    {
        return dimensionName switch
        {
            ModuleConstants.Dimensions.SearchTerm => SampleSearchTerms[seed % SampleSearchTerms.Length],
            ModuleConstants.Dimensions.ItemId => SampleItemCodes[seed % SampleItemCodes.Length],
            ModuleConstants.Dimensions.ItemName => SampleItemNames[seed % SampleItemNames.Length],
            ModuleConstants.Dimensions.ItemListName => SampleItemListNames[seed % SampleItemListNames.Length],
            ModuleConstants.UserDimensions.SessionKind => ModuleConstants.SessionKinds.Self,
            _ => $"{dimensionName}-{seed % GeneratedDimensionValueVariants}",
        };
    }

    private static string GetFilterSignature(AnalyticsDataQuery query)
    {
        return string.Join(";", (query.DimensionFilters ?? Array.Empty<AnalyticsDimensionFilter>())
            .Where(x => !string.IsNullOrEmpty(x.DimensionName))
            .OrderBy(x => x.DimensionName, StringComparer.Ordinal)
            .Select(x => $"{x.DimensionName}={string.Join(",", x.Values ?? Array.Empty<string>())}"));
    }

    // FNV-1a: string.GetHashCode is randomized per process, so it cannot seed deterministic sample data.
    private static int GetStableHash(params string[] parts)
    {
        var hash = FnvOffsetBasis;

        foreach (var character in string.Join("|", parts))
        {
            unchecked
            {
                hash ^= character;
                hash *= FnvPrime;
            }
        }

        return (int)(hash % int.MaxValue);
    }

    private static DateTime TruncateToHour(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTime(utcValue.Year, utcValue.Month, utcValue.Day, utcValue.Hour, 0, 0, DateTimeKind.Utc);
    }
}
