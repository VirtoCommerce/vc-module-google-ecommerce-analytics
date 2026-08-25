using System;
using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public abstract class AnalyticsEventCriteriaBase
{
    public string StoreId { get; set; }

    public IList<string> EventNames { get; set; } = new List<string>();

    public IList<AnalyticsDimensionFilter> DimensionFilters { get; set; } = new List<AnalyticsDimensionFilter>();

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
