using System;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Exceptions;

// Every IAnalyticsService read failure arrives as this — a store with no property id, a refused credential, a
// Google outage. The message names the store and the operation and nothing else: the property id, the settings
// and Google's own response stay in this module's log, because a consumer surfacing the failure has no way to
// know how far its own error surface travels.
public class AnalyticsException : Exception
{
    public AnalyticsException(string message)
        : base(message)
    {
    }

    public AnalyticsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
