using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

// Every dimension filter a read sends to Google is built here, including the ones carrying a consumer's data
// isolation, and both guards below have two callers. Reached only indirectly, they were pinned by neither.
public class AnalyticsFilterBuilderTests
{
    private const string ParamName = "criteria";

    [Fact]
    public void PropertyName_PrefixesTheNumericId()
    {
        Assert.Equal("properties/123456", AnalyticsFilterBuilder.PropertyName("123456"));
    }

    // Dropped rather than refused, a nameless filter widens the read to every organization.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateDimensionFilters_NoDimensionName_Throws(string dimensionName)
    {
        var filters = CreateFilters(new AnalyticsDimensionFilter { DimensionName = dimensionName, Values = ["org1"] });

        var exception = Assert.Throws<ArgumentException>(() => AnalyticsFilterBuilder.ValidateDimensionFilters(filters, ParamName));

        Assert.Equal(ParamName, exception.ParamName);
    }

    [Fact]
    public void ValidateDimensionFilters_NullFilterInTheList_Throws()
    {
        var filters = CreateFilters((AnalyticsDimensionFilter)null);

        Assert.Throws<ArgumentException>(() => AnalyticsFilterBuilder.ValidateDimensionFilters(filters, ParamName));
    }

    [Fact]
    public void ValidateDimensionFilters_NoValues_ThrowsNamingTheDimension()
    {
        var filters = CreateFilters(new AnalyticsDimensionFilter
        {
            DimensionName = ModuleConstants.UserDimensions.OrganizationId,
            Values = [],
        });

        var exception = Assert.Throws<ArgumentException>(() => AnalyticsFilterBuilder.ValidateDimensionFilters(filters, ParamName));

        // The message has to say WHICH filter: a consumer builds several and only one of them is wrong.
        Assert.Contains(ModuleConstants.UserDimensions.OrganizationId, exception.Message);
    }

    // The guard refuses a filter that pretends to scope and does not, not the absence of one.
    [Fact]
    public void ValidateDimensionFilters_NullOrEmptyList_DoesNotThrow()
    {
        AnalyticsFilterBuilder.ValidateDimensionFilters(null, ParamName);
        AnalyticsFilterBuilder.ValidateDimensionFilters([], ParamName);
    }

    [Fact]
    public void ValidateDimensionFilters_EveryFilterScoped_DoesNotThrow()
    {
        var filters = CreateFilters(
            new AnalyticsDimensionFilter { DimensionName = ModuleConstants.UserDimensions.SessionKind, Values = ["self"] },
            new AnalyticsDimensionFilter { DimensionName = ModuleConstants.UserDimensions.OrganizationId, Values = ["org1", "org2"] });

        AnalyticsFilterBuilder.ValidateDimensionFilters(filters, ParamName);
    }

    // The second filter is the malformed one: a guard that only looks at the first would pass this.
    [Fact]
    public void ValidateDimensionFilters_ChecksEveryFilterNotJustTheFirst()
    {
        var filters = CreateFilters(
            new AnalyticsDimensionFilter { DimensionName = ModuleConstants.UserDimensions.SessionKind, Values = ["self"] },
            new AnalyticsDimensionFilter { DimensionName = ModuleConstants.UserDimensions.OrganizationId, Values = [] });

        Assert.Throws<ArgumentException>(() => AnalyticsFilterBuilder.ValidateDimensionFilters(filters, ParamName));
    }

    [Theory]
    [InlineData(ModuleConstants.Dimensions.ItemId)]
    [InlineData(ModuleConstants.Dimensions.ItemName)]
    [InlineData(ModuleConstants.Dimensions.ItemListName)]
    public void HasItemDimensions_NamedAsADimension_IsTrue(string dimensionName)
    {
        Assert.True(AnalyticsFilterBuilder.HasItemDimensions([dimensionName], null));
    }

    // A summary carries no DimensionNames, so a filter is the only way it reaches the item scope.
    [Fact]
    public void HasItemDimensions_NamedInAFilter_IsTrue()
    {
        var filters = CreateFilters(new AnalyticsDimensionFilter
        {
            DimensionName = ModuleConstants.Dimensions.ItemId,
            Values = ["SKU-1"],
        });

        Assert.True(AnalyticsFilterBuilder.HasItemDimensions(null, filters));
    }

    [Fact]
    public void HasItemDimensions_EventScopedOnly_IsFalse()
    {
        var filters = CreateFilters(new AnalyticsDimensionFilter
        {
            DimensionName = ModuleConstants.UserDimensions.OrganizationId,
            Values = ["org1"],
        });

        Assert.False(AnalyticsFilterBuilder.HasItemDimensions([ModuleConstants.Dimensions.SearchTerm], filters));
        Assert.False(AnalyticsFilterBuilder.HasItemDimensions(null, null));
    }

    // A null entry must not fault the scope check the way it would a plain Contains over the names.
    [Fact]
    public void HasItemDimensions_NullFilterInTheList_IsFalse()
    {
        Assert.False(AnalyticsFilterBuilder.HasItemDimensions(null, CreateFilters((AnalyticsDimensionFilter)null)));
    }

    // GA4 field names are case-sensitive, so the prefix is applied on an exact match only.
    [Fact]
    public void MapDimensionName_KnownUserDimension_GetsThePrefix()
    {
        Assert.Equal(
            ModuleConstants.UserDimensions.Prefix + ModuleConstants.UserDimensions.OrganizationId,
            AnalyticsFilterBuilder.MapDimensionName(ModuleConstants.UserDimensions.OrganizationId));
    }

    [Theory]
    [InlineData(ModuleConstants.Dimensions.SearchTerm)]
    [InlineData("ORGANIZATION_ID")]
    public void MapDimensionName_NotAKnownUserDimension_IsPassedThrough(string dimensionName)
    {
        Assert.Equal(dimensionName, AnalyticsFilterBuilder.MapDimensionName(dimensionName));
    }

    // Diagnostics probes a caller-supplied dimension that is not one of the five built-in names.
    [Fact]
    public void MapDimensionName_ExtraUserDimension_GetsThePrefix()
    {
        Assert.Equal(
            ModuleConstants.UserDimensions.Prefix + "tenant_id",
            AnalyticsFilterBuilder.MapDimensionName("tenant_id", ["tenant_id"]));
    }

    [Fact]
    public void CreateInListExpression_CarriesTheFieldAndEveryValue()
    {
        var expression = AnalyticsFilterBuilder.CreateInListExpression("customUser:organization_id", ["org1", "org2"]);

        Assert.Equal("customUser:organization_id", expression.Filter.FieldName);
        Assert.Equal(["org1", "org2"], expression.Filter.InListFilter.Values);
    }

    [Fact]
    public void Combine_NoExpressions_IsNull()
    {
        Assert.Null(AnalyticsFilterBuilder.Combine(null));
        Assert.Null(AnalyticsFilterBuilder.Combine([]));
    }

    // One filter needs no AND group, and wrapping it would change the request shape for no reason.
    [Fact]
    public void Combine_OneExpression_IsReturnedUnwrapped()
    {
        var expression = AnalyticsFilterBuilder.CreateInListExpression("eventName", ["search"]);

        Assert.Same(expression, AnalyticsFilterBuilder.Combine([expression]));
    }

    // AND across filters is the isolation rule: session kind AND organization, never either one alone.
    [Fact]
    public void Combine_SeveralExpressions_AndsThemTogether()
    {
        var first = AnalyticsFilterBuilder.CreateInListExpression("customUser:session_kind", ["self"]);
        var second = AnalyticsFilterBuilder.CreateInListExpression("customUser:organization_id", ["org1"]);

        var combined = AnalyticsFilterBuilder.Combine([first, second]);

        Assert.NotNull(combined.AndGroup);
        Assert.Equal(2, combined.AndGroup.Expressions.Count);
        Assert.Equal(
            ["customUser:organization_id", "customUser:session_kind"],
            combined.AndGroup.Expressions.Select(x => x.Filter.FieldName).OrderBy(x => x, StringComparer.Ordinal));
    }

    private static IList<AnalyticsDimensionFilter> CreateFilters(params AnalyticsDimensionFilter[] filters)
    {
        return [.. filters];
    }
}
