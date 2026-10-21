using System;
using System.Collections.Generic;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsEvent : ICloneable
{
    public string EventName { get; set; }

    public DateTime? OccurredAt { get; set; }

    public int Count { get; set; }

    public IDictionary<string, string> Dimensions { get; set; } = new Dictionary<string, string>();

    public virtual object Clone()
    {
        var result = (AnalyticsEvent)MemberwiseClone();
        result.Dimensions = Dimensions != null ? new Dictionary<string, string>(Dimensions) : null;
        return result;
    }
}
