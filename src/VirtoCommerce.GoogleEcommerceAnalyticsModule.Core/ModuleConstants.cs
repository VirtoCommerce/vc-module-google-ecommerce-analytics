using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core
{
    public static class ModuleConstants
    {
        public static class Settings
        {
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

                public static IEnumerable<SettingDescriptor> AllSettings
                {
                    get
                    {
                        yield return EnableTracking;
                        yield return MeasurementId;
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
