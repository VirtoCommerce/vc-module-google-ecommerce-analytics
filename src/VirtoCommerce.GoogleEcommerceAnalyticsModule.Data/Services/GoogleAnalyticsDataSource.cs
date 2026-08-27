using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class GoogleAnalyticsDataSource : IAnalyticsDataSource
{
    private const string NotSetValue = "(not set)";
    private const string UserDimensionPrefix = "customUser:";
    private const string EventCountMetric = "eventCount";
    private const string ItemsViewedMetric = "itemsViewed";
    private const string DateHourFormat = "yyyyMMddHH";
    private const string DateFormat = "yyyy-MM-dd";
    private const string DefaultStartDate = "2015-08-14";
    private const string DefaultEndDate = "today";

    private static readonly string[] ItemDimensionNames =
    {
        ModuleConstants.Dimensions.ItemId,
        ModuleConstants.Dimensions.ItemName,
        ModuleConstants.Dimensions.ItemListName,
    };

    private readonly IGoogleAnalyticsReportClient _reportClient;

    public GoogleAnalyticsDataSource(IGoogleAnalyticsReportClient reportClient)
    {
        _reportClient = reportClient;
    }

    public virtual Task<AnalyticsEventSearchResult> GetRowsAsync(AnalyticsDataQuery query)
    {
        return HasItemDimensions(query) ? GetItemScopedRowsAsync(query) : GetEventScopedRowsAsync(query);
    }

    protected virtual async Task<AnalyticsEventSearchResult> GetEventScopedRowsAsync(AnalyticsDataQuery query)
    {
        var response = await _reportClient.RunReportAsync(BuildEventReportRequest(query));
        return MapResponse(response, query);
    }

    // GA4 compatibility of item-scoped dimensions combined with user-scoped custom dimensions is unverified;
    // kept as a separate path so it can be reworked without touching the main query path.
    protected virtual async Task<AnalyticsEventSearchResult> GetItemScopedRowsAsync(AnalyticsDataQuery query)
    {
        var response = await _reportClient.RunReportAsync(BuildItemReportRequest(query));
        var result = MapResponse(response, query);

        var eventName = query.EventNames?.Count == 1 ? query.EventNames[0] : null;
        if (!string.IsNullOrEmpty(eventName))
        {
            foreach (var analyticsEvent in result.Events)
            {
                analyticsEvent.EventName = eventName;
            }
        }

        return result;
    }

    public virtual RunReportRequest BuildEventReportRequest(AnalyticsDataQuery query)
    {
        var request = CreateRequest(query);

        request.Dimensions.Add(new Dimension { Name = ModuleConstants.Dimensions.EventName });
        AddDateHourAndExtraDimensions(request, query);

        request.Metrics.Add(new Metric { Name = EventCountMetric });
        AddOrderBy(request, query, EventCountMetric);

        return request;
    }

    // Item-scoped report shape per the GA4 schema: metric itemsViewed, eventName used only as a dimension filter.
    public virtual RunReportRequest BuildItemReportRequest(AnalyticsDataQuery query)
    {
        var request = CreateRequest(query);

        AddDateHourAndExtraDimensions(request, query);

        request.Metrics.Add(new Metric { Name = ItemsViewedMetric });
        AddOrderBy(request, query, ItemsViewedMetric);

        return request;
    }

    protected virtual RunReportRequest CreateRequest(AnalyticsDataQuery query)
    {
        var request = new RunReportRequest
        {
            Property = $"properties/{query.PropertyId}",
            Limit = query.Take > 0 ? query.Take : 1,
            Offset = query.Skip,
        };

        request.DateRanges.Add(new DateRange
        {
            StartDate = query.From?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? DefaultStartDate,
            EndDate = query.To?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? DefaultEndDate,
        });

        var dimensionFilter = BuildDimensionFilter(query);
        if (dimensionFilter != null)
        {
            request.DimensionFilter = dimensionFilter;
        }

        return request;
    }

    protected virtual void AddDateHourAndExtraDimensions(RunReportRequest request, AnalyticsDataQuery query)
    {
        if (!IsCountSort(query))
        {
            request.Dimensions.Add(new Dimension { Name = ModuleConstants.Dimensions.DateHour });
        }

        foreach (var dimensionName in (query.DimensionNames ?? Array.Empty<string>())
                     .Where(x => x != ModuleConstants.Dimensions.EventName && x != ModuleConstants.Dimensions.DateHour))
        {
            request.Dimensions.Add(new Dimension { Name = MapDimensionName(dimensionName) });
        }
    }

    protected virtual void AddOrderBy(RunReportRequest request, AnalyticsDataQuery query, string metricName)
    {
        request.OrderBys.Add(IsCountSort(query)
            ? new OrderBy
            {
                Desc = true,
                Metric = new OrderBy.Types.MetricOrderBy { MetricName = metricName },
            }
            : new OrderBy
            {
                Desc = true,
                Dimension = new OrderBy.Types.DimensionOrderBy { DimensionName = ModuleConstants.Dimensions.DateHour },
            });
    }

    protected virtual FilterExpression BuildDimensionFilter(AnalyticsDataQuery query)
    {
        var expressions = new List<FilterExpression>();

        if (query.EventNames?.Count > 0)
        {
            expressions.Add(CreateInListExpression(ModuleConstants.Dimensions.EventName, query.EventNames));
        }

        foreach (var filter in (query.DimensionFilters ?? Array.Empty<AnalyticsDimensionFilter>())
                     .Where(x => !string.IsNullOrEmpty(x.DimensionName) && x.Values?.Count > 0))
        {
            expressions.Add(CreateInListExpression(MapDimensionName(filter.DimensionName), filter.Values));
        }

        if (expressions.Count == 0)
        {
            return null;
        }

        if (expressions.Count == 1)
        {
            return expressions[0];
        }

        var result = new FilterExpression { AndGroup = new FilterExpressionList() };
        result.AndGroup.Expressions.AddRange(expressions);
        return result;
    }

    protected virtual AnalyticsEventSearchResult MapResponse(RunReportResponse response, AnalyticsDataQuery query)
    {
        var result = AbstractTypeFactory<AnalyticsEventSearchResult>.TryCreateInstance();
        result.TotalCount = response.RowCount;

        if (query.Take <= 0)
        {
            return result;
        }

        var headers = response.DimensionHeaders.Select(x => x.Name).ToList();

        foreach (var row in response.Rows)
        {
            var analyticsEvent = AbstractTypeFactory<AnalyticsEvent>.TryCreateInstance();

            for (var i = 0; i < headers.Count && i < row.DimensionValues.Count; i++)
            {
                MapDimensionValue(analyticsEvent, headers[i], row.DimensionValues[i].Value);
            }

            analyticsEvent.Count = row.MetricValues.Count > 0
                && int.TryParse(row.MetricValues[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;

            result.Events.Add(analyticsEvent);
        }

        return result;
    }

    protected virtual void MapDimensionValue(AnalyticsEvent analyticsEvent, string dimensionName, string value)
    {
        if (string.IsNullOrEmpty(value) || value == NotSetValue)
        {
            return;
        }

        if (dimensionName == ModuleConstants.Dimensions.EventName)
        {
            analyticsEvent.EventName = value;
        }
        else if (dimensionName == ModuleConstants.Dimensions.DateHour)
        {
            analyticsEvent.OccurredAt = ParseDateHour(value);
        }
        else
        {
            analyticsEvent.Dimensions[UnmapDimensionName(dimensionName)] = value;
        }
    }

    protected virtual string MapDimensionName(string dimensionName)
    {
        return ModuleConstants.UserDimensions.AllNames.Contains(dimensionName)
            ? UserDimensionPrefix + dimensionName
            : dimensionName;
    }

    protected virtual string UnmapDimensionName(string dimensionName)
    {
        return dimensionName.StartsWith(UserDimensionPrefix, StringComparison.Ordinal)
            ? dimensionName[UserDimensionPrefix.Length..]
            : dimensionName;
    }

    protected virtual bool HasItemDimensions(AnalyticsDataQuery query)
    {
        return query.DimensionNames?.Any(x => ItemDimensionNames.Contains(x)) == true
            || query.DimensionFilters?.Any(x => ItemDimensionNames.Contains(x.DimensionName)) == true;
    }

    protected static bool IsCountSort(AnalyticsDataQuery query)
    {
        return ModuleConstants.SortBy.Count.EqualsIgnoreCase(query.SortBy);
    }

    private static FilterExpression CreateInListExpression(string fieldName, IEnumerable<string> values)
    {
        var inListFilter = new Filter.Types.InListFilter();
        inListFilter.Values.AddRange(values);

        return new FilterExpression
        {
            Filter = new Filter { FieldName = fieldName, InListFilter = inListFilter },
        };
    }

    private static DateTime? ParseDateHour(string value)
    {
        return DateTime.TryParseExact(value, DateHourFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : null;
    }
}
