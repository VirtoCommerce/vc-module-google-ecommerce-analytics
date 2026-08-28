using System.Collections.Generic;
using System.Linq;
using Google.Analytics.Data.V1Beta;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

internal static class AnalyticsFilterBuilder
{
    public static string PropertyName(string propertyId)
    {
        return $"properties/{propertyId}";
    }

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
