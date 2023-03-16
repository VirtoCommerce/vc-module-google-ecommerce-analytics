using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core
{
    public static class ModuleConstants
    {
        public static class Security
        {
            public static class Permissions
            {
                public const string Access = "googleanalytics:access";

                public static string[] AllPermissions { get; } = { Access };
            }
        }

        public static class Settings
        {
            public const string DefaultGoogleAnalyticsUrl = "https://analytics.google.com/analytics/web/";

            public static class General
            {
                public static SettingDescriptor EnableTracking { get; } = new SettingDescriptor
                {
                    Name = "GoogleAnalytics4.EnableTracking",
                    GroupName = "Google Analytics 4",
                    ValueType = SettingValueType.Boolean
                };

                public static SettingDescriptor MeasurementId { get; } = new SettingDescriptor
                {
                    Name = "GoogleAnalytics4.MeasurementId",
                    GroupName = "Google Analytics 4",
                    ValueType = SettingValueType.ShortText
                };

                public static SettingDescriptor GoogleAnalyticsUrl { get; } = new SettingDescriptor
                {
                    Name = "GoogleAnalytics4.GoogleAnalyticsUrl",
                    GroupName = "Google Analytics 4",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = DefaultGoogleAnalyticsUrl
                };

                public static IEnumerable<SettingDescriptor> AllSettings
                {
                    get
                    {
                        yield return EnableTracking;
                        yield return MeasurementId;
                        yield return GoogleAnalyticsUrl;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> StoreLevelSettings
            {
                get
                {
                    yield return General.EnableTracking;
                    yield return General.MeasurementId;
                }
            }
        }
    }
}
