using System;
using System.Collections.Generic;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsDataQuery
{
    public string PropertyId { get; set; }

    public string CredentialJson { get; set; }

    public IList<string> EventNames { get; set; } = new List<string>();

    public IList<AnalyticsDimensionFilter> DimensionFilters { get; set; } = new List<AnalyticsDimensionFilter>();

    public IList<string> DimensionNames { get; set; } = new List<string>();

    public string SortBy { get; set; } = ModuleConstants.SortBy.Date;

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Take { get; set; }

    public int Skip { get; set; }
}
