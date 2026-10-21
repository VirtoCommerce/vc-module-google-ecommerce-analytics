using System;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Exceptions;

// Every IAnalyticsService failure arrives as this — no property id, a refused credential, a Google outage. The
// message names the store and the operation and nothing else; the cause stays in this module's log, because a
// consumer surfacing it cannot know how far its own error surface travels.
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
