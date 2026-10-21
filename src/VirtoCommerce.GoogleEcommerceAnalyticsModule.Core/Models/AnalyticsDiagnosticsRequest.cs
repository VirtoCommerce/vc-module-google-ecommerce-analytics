using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsDiagnosticsRequest
{
    public IList<string> UserDimensionNames { get; set; } = [];

    public IList<string> EventNames { get; set; } = [];

    public IList<AnalyticsDiagnosticsReportShape> Reports { get; set; } = [];

    public bool IncludeLiveData { get; set; } = true;
}
