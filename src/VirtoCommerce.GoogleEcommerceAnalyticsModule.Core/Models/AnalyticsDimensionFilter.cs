using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsDimensionFilter
{
    public string DimensionName { get; set; }

    public IList<string> Values { get; set; } = new List<string>();
}
