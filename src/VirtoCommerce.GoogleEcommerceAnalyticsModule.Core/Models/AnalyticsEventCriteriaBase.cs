using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public abstract class AnalyticsEventCriteriaBase : SearchCriteriaBase
{
    public string StoreId { get; set; }

    public IList<string> EventNames { get; set; } = [];

    public IList<AnalyticsDimensionFilter> DimensionFilters { get; set; } = [];

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
