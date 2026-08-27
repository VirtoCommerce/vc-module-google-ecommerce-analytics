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

        public static class EventNames
        {
            public const string Search = "search";
            public const string ViewSearchResults = "view_search_results";
            public const string ViewItem = "view_item";
            public const string Login = "login";
            public const string SignUp = "sign_up";
            public const string AddToCart = "add_to_cart";
            public const string Purchase = "purchase";
        }

        public static class UserDimensions
        {
            public const string ContactId = "contact_id";
            public const string OrganizationId = "organization_id";
            public const string OrganizationName = "organization_name";
            public const string IsSalesRep = "is_sales_rep";
            public const string SessionKind = "session_kind";

            public static string[] AllNames { get; } = { ContactId, OrganizationId, OrganizationName, IsSalesRep, SessionKind };
        }

        public static class SessionKinds
        {
            public const string Self = "self";
            public const string Impersonated = "impersonated";
        }

        public static class Dimensions
        {
            public const string SearchTerm = "searchTerm";
            public const string ItemId = "itemId";
            public const string ItemName = "itemName";
            public const string ItemListName = "itemListName";
            public const string EventName = "eventName";
            public const string DateHour = "dateHour";
        }

        public static class SortBy
        {
            public const string Date = "date";
            public const string Count = "count";
        }

        public static class DiagnosticsStages
        {
            public const string Configuration = "configuration";
            public const string Credentials = "credentials";
            public const string ApiAccess = "apiAccess";
            public const string CustomDimensions = "customDimensions";
            public const string ReportCompatibility = "reportCompatibility";
            public const string Realtime = "realtime";
            public const string ProcessedData = "processedData";
        }

        public static class DiagnosticsStatuses
        {
            public const string Passed = "Passed";
            public const string Warning = "Warning";
            public const string Failed = "Failed";
            public const string Skipped = "Skipped";
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
                    ValueType = SettingValueType.Boolean,
                    IsPublic = true
                };

                public static SettingDescriptor MeasurementId { get; } = new SettingDescriptor
                {
                    Name = "GoogleAnalytics4.MeasurementId",
                    GroupName = "Google Analytics 4",
                    ValueType = SettingValueType.ShortText,
                    IsPublic = true
                };

                public static SettingDescriptor GtmContainerId { get; } = new SettingDescriptor
                {
                    Name = "GoogleAnalytics4.GtmContainerId",
                    GroupName = "Google Analytics 4",
                    ValueType = SettingValueType.ShortText,
                    IsPublic = true
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
                        yield return GtmContainerId;
                    }
                }
            }

            public static class DataApi
            {
                public const int DefaultCacheTtlMinutes = 60;

                public static SettingDescriptor PropertyId { get; } = new SettingDescriptor
                {
                    Name = "GoogleAnalytics4.DataApi.PropertyId",
                    GroupName = "Google Analytics 4",
                    ValueType = SettingValueType.ShortText
                };

                public static SettingDescriptor CacheTtlMinutes { get; } = new SettingDescriptor
                {
                    Name = "GoogleAnalytics4.DataApi.CacheTtlMinutes",
                    GroupName = "Google Analytics 4",
                    ValueType = SettingValueType.PositiveInteger,
                    DefaultValue = DefaultCacheTtlMinutes
                };

                public static IEnumerable<SettingDescriptor> AllSettings
                {
                    get
                    {
                        yield return PropertyId;
                        yield return CacheTtlMinutes;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> StoreLevelSettings
            {
                get
                {
                    yield return General.EnableTracking;
                    yield return General.MeasurementId;
                    yield return General.GtmContainerId;
                    yield return DataApi.PropertyId;
                    yield return DataApi.CacheTtlMinutes;
                }
            }
        }
    }
}
