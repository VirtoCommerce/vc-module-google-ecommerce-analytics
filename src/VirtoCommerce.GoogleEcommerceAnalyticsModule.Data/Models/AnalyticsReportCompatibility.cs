using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

public class AnalyticsReportCompatibility
{
    public string Report { get; set; }

    public bool Compatible { get; set; }

    public IList<string> IncompatibleDimensions { get; set; } = new List<string>();

    public IList<string> IncompatibleMetrics { get; set; } = new List<string>();

    public string ErrorMessage { get; set; }
}
