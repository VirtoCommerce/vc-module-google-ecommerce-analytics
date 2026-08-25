using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsEventSearchCriteria : AnalyticsEventCriteriaBase
{
    public IList<string> DimensionNames { get; set; } = new List<string>();

    public string SortBy { get; set; } = ModuleConstants.SortBy.Date;

    public int Take { get; set; } = 20;

    public int Skip { get; set; }
}
