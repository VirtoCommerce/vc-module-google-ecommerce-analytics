using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsDiagnosticsResult
{
    public IList<AnalyticsDiagnosticsCheck> Checks { get; set; } = [];
}
