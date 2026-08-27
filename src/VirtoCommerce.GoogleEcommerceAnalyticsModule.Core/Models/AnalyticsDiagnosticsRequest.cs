using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsDiagnosticsRequest
{
    public IList<string> UserDimensionNames { get; set; } = new List<string>();

    public IList<string> EventNames { get; set; } = new List<string>();

    public IList<AnalyticsDiagnosticsReportShape> Reports { get; set; } = new List<AnalyticsDiagnosticsReportShape>();

    public bool IncludeLiveData { get; set; } = true;
}
