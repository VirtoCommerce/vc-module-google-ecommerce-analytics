using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

public class AnalyticsEventSearchResult : ICloneable
{
    public int TotalCount { get; set; }

    public IList<AnalyticsEvent> Events { get; set; } = [];

    public virtual object Clone()
    {
        var result = (AnalyticsEventSearchResult)MemberwiseClone();
        result.Events = Events?.Select(x => x.CloneTyped()).ToList();
        return result;
    }
}
