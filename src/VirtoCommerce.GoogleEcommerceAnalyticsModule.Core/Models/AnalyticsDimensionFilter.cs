using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsDimensionFilter : ValueObject
{
    public string DimensionName { get; set; }

    public IList<string> Values { get; set; } = [];
}
