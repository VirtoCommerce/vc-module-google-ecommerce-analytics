using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsCompatibilityResult
{
    public bool Available { get; set; }

    public string ErrorMessage { get; set; }

    public IList<AnalyticsReportCompatibility> Reports { get; set; } = new List<AnalyticsReportCompatibility>();
}
