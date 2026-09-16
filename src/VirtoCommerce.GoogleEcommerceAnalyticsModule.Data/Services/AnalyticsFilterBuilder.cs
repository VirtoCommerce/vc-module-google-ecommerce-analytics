using System;
using System.Collections.Generic;
using System.Linq;
using Google.Analytics.Data.V1Beta;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

internal static class AnalyticsFilterBuilder
{
    // Naming any of these switches the report to the item scope, where Count means items viewed and the rows
    // carry no eventName of their own.
    private static readonly string[] ItemDimensionNames =
    [
        ModuleConstants.Dimensions.ItemId,
        ModuleConstants.Dimensions.ItemName,
        ModuleConstants.Dimensions.ItemListName,
    ];

    public static string PropertyName(string propertyId)
    {
        return $"properties/{propertyId}";
    }

    // One implementation, two callers on purpose: the service runs it as an argument check, before configuration
    // and before the cache, so a caller-caused refusal is never cached; the data source runs it again as the last
    // place that can refuse an unscoped read reaching Google.
    public static void ValidateDimensionFilters(IList<AnalyticsDimensionFilter> filters, string paramName)
    {
        foreach (var filter in filters ?? [])
        {
            // Dropped rather than refused, either of these widens the read instead of narrowing it — and these
            // filters carry the consumer's data isolation.
            if (string.IsNullOrWhiteSpace(filter?.DimensionName))
            {
                throw new ArgumentException(
                    "A dimension filter carries no dimension name, which would leave the read unscoped.",
                    paramName);
            }

            if (filter.Values.IsNullOrEmpty())
            {
                throw new ArgumentException(
                    $"Dimension filter '{filter.DimensionName}' carries no values, which would leave the read unscoped.",
                    paramName);
            }
        }
    }

    public static bool HasItemDimensions(IList<string> dimensionNames, IList<AnalyticsDimensionFilter> dimensionFilters)
    {
        return dimensionNames?.Any(ItemDimensionNames.Contains) == true
            || dimensionFilters?.Any(x => ItemDimensionNames.Contains(x?.DimensionName)) == true;
    }

    // Ordinal on purpose: GA4 field names are case-sensitive, so a mis-cased name is rejected by the API
    // whether or not it is prefixed, and the response echoes back the exact name that was sent.
    public static string MapDimensionName(string dimensionName, IList<string> extraUserDimensionNames = null)
    {
        return ModuleConstants.UserDimensions.AllNames.Contains(dimensionName) || extraUserDimensionNames?.Contains(dimensionName) == true
            ? ModuleConstants.UserDimensions.Prefix + dimensionName
            : dimensionName;
    }

    public static FilterExpression CreateInListExpression(string fieldName, IEnumerable<string> values)
    {
        var inListFilter = new Filter.Types.InListFilter();
        inListFilter.Values.AddRange(values);

        return new FilterExpression
        {
            Filter = new Filter { FieldName = fieldName, InListFilter = inListFilter },
        };
    }

    public static FilterExpression Combine(IList<FilterExpression> expressions)
    {
        if (expressions.IsNullOrEmpty())
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
}
