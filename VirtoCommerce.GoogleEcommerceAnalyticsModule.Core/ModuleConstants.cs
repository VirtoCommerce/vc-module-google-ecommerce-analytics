using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core
{
    public static class ModuleConstants
    {
        //public static class Security
        //{
        //    public static class Permissions
        //    {
        //        public const string Access = "virtoCommerceGoogleEcommerceAnalyticsModule:access";
        //        public const string Create = "virtoCommerceGoogleEcommerceAnalyticsModule:create";
        //        public const string Read = "virtoCommerceGoogleEcommerceAnalyticsModule:read";
        //        public const string Update = "virtoCommerceGoogleEcommerceAnalyticsModule:update";
        //        public const string Delete = "virtoCommerceGoogleEcommerceAnalyticsModule:delete";

        //        public static string[] AllPermissions { get; } = { Read, Create, Access, Update, Delete };
        //    }
        //}

        public static class Settings
        {
            public static class General
            {
                public static SettingDescriptor EnableTracking { get; } = new SettingDescriptor
                {
                    Name = "GoogleEcommerceAnalytics.EnableTracking",
                    GroupName = "Google Ecommerce Analytics",
                    ValueType = SettingValueType.Boolean
                };

                public static SettingDescriptor GoogleTagManagerId { get; } = new SettingDescriptor
                {
                    Name = "GoogleEcommerceAnalytics.GoogleTagManagerId",
                    GroupName = "Google Ecommerce Analytics",
                    ValueType = SettingValueType.ShortText
                };

                public static SettingDescriptor GoogleAnalyticsTrackingId { get; } = new SettingDescriptor
                {
                    Name = "GoogleEcommerceAnalytics.GoogleAnalyticsTrackingId",
                    GroupName = "Google Ecommerce Analytics",
                    ValueType = SettingValueType.ShortText
                };

                public static SettingDescriptor CreateECommerceTransaction { get; } = new SettingDescriptor
                {
                    Name = "GoogleEcommerceAnalytics.CreateECommerceTransaction",
                    GroupName = "Google Ecommerce Analytics",
                    ValueType = SettingValueType.Boolean
                };

                public static SettingDescriptor ReverseECommerceTransaction { get; } = new SettingDescriptor
                {
                    Name = "GoogleEcommerceAnalytics.ReverseECommerceTransaction",
                    GroupName = "Google Ecommerce Analytics",
                    ValueType = SettingValueType.Boolean
                };




                public static SettingDescriptor VirtoCommerceGoogleEcommerceAnalyticsModulePassword { get; } = new SettingDescriptor
                {
                    Name = "VirtoCommerceGoogleEcommerceAnalyticsModule.VirtoCommerceGoogleEcommerceAnalyticsModulePassword",
                    GroupName = "Google Ecommerce Analytics",
                    ValueType = SettingValueType.SecureString,
                    DefaultValue = "qwerty"
                };

                public static IEnumerable<SettingDescriptor> AllSettings
                {
                    get
                    {
                        yield return EnableTracking;
                        yield return GoogleTagManagerId;
                        yield return GoogleAnalyticsTrackingId;
                        yield return CreateECommerceTransaction;
                        yield return ReverseECommerceTransaction;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> AllSettings
            {
                get
                {
                    return General.AllSettings;
                }
            }
        }
    }
}
