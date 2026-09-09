using System;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsEventSummary : ICloneable
{
    public string EventName { get; set; }

    public int TotalCount { get; set; }

    public DateTime? LastOccurredAt { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
