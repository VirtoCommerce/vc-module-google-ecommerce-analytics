using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsDiagnosticsReportShape
{
    public string Name { get; set; }

    public IList<string> DimensionNames { get; set; } = new List<string>();

    public string MetricName { get; set; }

    public IList<string> EventNames { get; set; } = new List<string>();
}
