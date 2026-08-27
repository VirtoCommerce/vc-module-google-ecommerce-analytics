using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Google.Api.Gax.Grpc;
using Grpc.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;
using ErrorInfo = Google.Rpc.ErrorInfo;
using Metadata = Google.Analytics.Data.V1Beta.Metadata;
using Stages = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.DiagnosticsStages;
using Statuses = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.DiagnosticsStatuses;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class AnalyticsDiagnosticsService : IAnalyticsDiagnosticsService
{
    private const string UserDimensionPrefix = "customUser:";
    private const string EventCountMetric = "eventCount";
    private const string CredentialTypeProperty = "type";
    private const string ProbeFilterValue = "diagnostics-probe";
    private const string ProcessedDataStartDate = "7daysAgo";
    private const string ProcessedDataEndDate = "today";
    private const int RealtimeMinutesAgo = 29;
    private const int LiveDataRowsLimit = 50;

    private const string ScopeInsufficientReason = "ACCESS_TOKEN_SCOPE_INSUFFICIENT";
    private const string ServiceDisabledReason = "SERVICE_DISABLED";
    private const string UserProjectDeniedReason = "USER_PROJECT_DENIED";
    private const string ConsumerMetadataKey = "consumer";
    private const string ProjectResourcePrefix = "projects/";

    private static readonly string[] AllStages =
    {
        Stages.Configuration,
        Stages.Credentials,
        Stages.ApiAccess,
        Stages.CustomDimensions,
        Stages.ReportCompatibility,
        Stages.Realtime,
        Stages.ProcessedData,
    };

    private readonly IAnalyticsSettingsResolver _settingsResolver;
    private readonly IGoogleAnalyticsReportClient _reportClient;

    public AnalyticsDiagnosticsService(IAnalyticsSettingsResolver settingsResolver, IGoogleAnalyticsReportClient reportClient)
    {
        _settingsResolver = settingsResolver;
        _reportClient = reportClient;
    }

    public virtual async Task<AnalyticsDiagnosticsResult> RunAsync(string storeId, AnalyticsDiagnosticsRequest request)
    {
        request ??= AbstractTypeFactory<AnalyticsDiagnosticsRequest>.TryCreateInstance();
        var result = AbstractTypeFactory<AnalyticsDiagnosticsResult>.TryCreateInstance();
        var checks = result.Checks;

        var settings = await CheckConfigurationAsync(storeId, checks);
        if (settings == null)
        {
            SkipRemainingStages(checks, checks[0].Status == Statuses.Warning
                ? "Skipped: sample data mode is active."
                : "Skipped: configuration check failed.");
            return result;
        }

        if (!await CheckCredentialsAsync(settings, checks))
        {
            SkipRemainingStages(checks, "Skipped: credential resolution failed.");
            return result;
        }

        var metadata = await CheckApiAccessAsync(settings, checks);
        if (metadata == null)
        {
            SkipRemainingStages(checks, "Skipped: Google Analytics Data API is not accessible.");
            return result;
        }

        CheckCustomDimensions(metadata, request, checks);
        await CheckReportCompatibilityAsync(settings, request, checks);
        await CheckLiveDataAsync(settings, request, checks);

        return result;
    }

    protected virtual async Task<AnalyticsDataApiSettings> CheckConfigurationAsync(string storeId, IList<AnalyticsDiagnosticsCheck> checks)
    {
        AnalyticsDataApiSettings settings;

        try
        {
            settings = await _settingsResolver.ResolveAsync(storeId);
        }
        catch (Exception ex)
        {
            AddCheck(checks, Stages.Configuration, Statuses.Failed,
                $"Failed to resolve Google Analytics settings for store '{storeId}'.", ex.Message);
            return null;
        }

        if (settings.SampleDataEnabled)
        {
            AddCheck(checks, Stages.Configuration, Statuses.Warning,
                "Sample data mode is active — Google Analytics is not exercised.");
            return null;
        }

        if (!settings.IsGoogleConfigured)
        {
            AddCheck(checks, Stages.Configuration, Statuses.Failed,
                "GA4 Data API property id is not configured for the store (GoogleAnalytics4.DataApi.PropertyId).");
            return null;
        }

        AddCheck(checks, Stages.Configuration, Statuses.Passed,
            $"Property {settings.PropertyId} configured; credential: {DescribeCredentialSource(settings.CredentialJson)}.");
        return settings;
    }

    protected virtual async Task<bool> CheckCredentialsAsync(AnalyticsDataApiSettings settings, IList<AnalyticsDiagnosticsCheck> checks)
    {
        try
        {
            await _reportClient.ValidateCredentialAsync(settings.CredentialJson);
            AddCheck(checks, Stages.Credentials, Statuses.Passed, "Credential resolved successfully.");
            return true;
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(settings.CredentialJson)
                ? "Application Default Credentials are not available — set GoogleAnalytics4.DataApi.ServiceAccountJson or provide ADC (GOOGLE_APPLICATION_CREDENTIALS)."
                : "The configured credential JSON cannot be used — check GoogleAnalytics4.DataApi.ServiceAccountJson for malformed or incomplete JSON.";
            AddCheck(checks, Stages.Credentials, Statuses.Failed, message, ex.Message);
            return false;
        }
    }

    protected virtual async Task<Metadata> CheckApiAccessAsync(AnalyticsDataApiSettings settings, IList<AnalyticsDiagnosticsCheck> checks)
    {
        try
        {
            var metadata = await _reportClient.GetMetadataAsync(settings.CredentialJson, settings.PropertyId);
            if (metadata == null)
            {
                AddCheck(checks, Stages.ApiAccess, Statuses.Failed, "Google Analytics Data API returned no metadata.");
                return null;
            }

            AddCheck(checks, Stages.ApiAccess, Statuses.Passed,
                $"Google Analytics Data API is reachable for property {settings.PropertyId}.");
            return metadata;
        }
        catch (RpcException ex)
        {
            AddCheck(checks, Stages.ApiAccess, Statuses.Failed, MapApiAccessFailure(ex, settings.PropertyId), DescribeError(ex));
            return null;
        }
        catch (Exception ex)
        {
            AddCheck(checks, Stages.ApiAccess, Statuses.Failed, "Google Analytics Data API call failed.", ex.Message);
            return null;
        }
    }

    protected virtual string MapApiAccessFailure(RpcException exception, string propertyId)
    {
        var errorInfo = exception.GetStatusDetail<ErrorInfo>();
        var reason = errorInfo?.Reason;

        if (reason == ScopeInsufficientReason)
        {
            return "The credential's access token lacks the required scope — re-authorize or re-issue it with the https://www.googleapis.com/auth/analytics.readonly scope.";
        }

        if (reason == ServiceDisabledReason)
        {
            var project = GetProjectName(errorInfo);
            var projectPart = string.IsNullOrEmpty(project) ? "the credential's project" : $"project {project}";
            return $"The Google Analytics Data API (analyticsdata.googleapis.com) is disabled in {projectPart} — enable it in the Google Cloud console and retry.";
        }

        if (reason == UserProjectDeniedReason || exception.Status.Detail?.Contains("quota project", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "The credential has no usable quota project — set a quota project for the credential or use a service account key from a project with the Data API enabled.";
        }

        if (exception.StatusCode == StatusCode.PermissionDenied)
        {
            return $"The credential has no access to GA4 property {propertyId} — grant its principal the Viewer role in GA4 Admin > Property access management.";
        }

        if (exception.StatusCode == StatusCode.NotFound)
        {
            return $"GA4 property {propertyId} was not found — check the GoogleAnalytics4.DataApi.PropertyId setting (use the numeric property id, not the measurement id).";
        }

        return "Google Analytics Data API call failed.";
    }

    protected virtual void CheckCustomDimensions(Metadata metadata, AnalyticsDiagnosticsRequest request, IList<AnalyticsDiagnosticsCheck> checks)
    {
        var requestedNames = GetUserDimensionNames(request);
        if (requestedNames.Count == 0)
        {
            AddCheck(checks, Stages.CustomDimensions, Statuses.Skipped, "No user dimensions requested.");
            return;
        }

        var registeredNames = metadata.Dimensions
            .Select(x => x.ApiName)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingNames = requestedNames.Where(x => !registeredNames.Contains(UserDimensionPrefix + x)).ToList();

        if (missingNames.Count == 0)
        {
            AddCheck(checks, Stages.CustomDimensions, Statuses.Passed,
                $"All requested user dimensions are registered: {string.Join(", ", requestedNames)}.");
        }
        else
        {
            AddCheck(checks, Stages.CustomDimensions, Statuses.Failed,
                $"Missing user-scoped custom dimensions: {string.Join(", ", missingNames)}. " +
                "Register them in GA4 Admin > Custom definitions; registration is not retroactive — data collection for reporting starts at registration.");
        }
    }

    protected virtual async Task CheckReportCompatibilityAsync(AnalyticsDataApiSettings settings, AnalyticsDiagnosticsRequest request, IList<AnalyticsDiagnosticsCheck> checks)
    {
        var shapes = (request.Reports ?? []).Where(x => x != null).ToList();
        if (shapes.Count == 0)
        {
            AddCheck(checks, Stages.ReportCompatibility, Statuses.Skipped, "No report shapes requested.");
            return;
        }

        var userDimensionNames = GetUserDimensionNames(request);
        var problems = new List<string>();

        try
        {
            foreach (var shape in shapes)
            {
                var compatibilityRequest = BuildCompatibilityRequest(settings.PropertyId, shape, userDimensionNames);
                var response = await _reportClient.CheckCompatibilityAsync(settings.CredentialJson, compatibilityRequest);
                var incompatibleFields = GetIncompatibleFields(response, GetRequestedFieldNames(compatibilityRequest));

                if (incompatibleFields.Count > 0)
                {
                    problems.Add($"{shape.Name}: {string.Join(", ", incompatibleFields)}");
                }
            }
        }
        catch (Exception ex)
        {
            AddCheck(checks, Stages.ReportCompatibility, Statuses.Failed, "Report compatibility check failed.", DescribeError(ex));
            return;
        }

        if (problems.Count == 0)
        {
            AddCheck(checks, Stages.ReportCompatibility, Statuses.Passed,
                $"Compatible report shapes: {string.Join(", ", shapes.Select(x => x.Name))}.");
        }
        else
        {
            AddCheck(checks, Stages.ReportCompatibility, Statuses.Failed,
                $"Incompatible fields — {string.Join("; ", problems)}.");
        }
    }

    protected virtual async Task CheckLiveDataAsync(AnalyticsDataApiSettings settings, AnalyticsDiagnosticsRequest request, IList<AnalyticsDiagnosticsCheck> checks)
    {
        if (!request.IncludeLiveData)
        {
            const string skipMessage = "Skipped: live-data checks disabled by request.";
            AddCheck(checks, Stages.Realtime, Statuses.Skipped, skipMessage);
            AddCheck(checks, Stages.ProcessedData, Statuses.Skipped, skipMessage);
            return;
        }

        await CheckRealtimeAsync(settings, request, checks);
        await CheckProcessedDataAsync(settings, request, checks);
    }

    protected virtual async Task CheckRealtimeAsync(AnalyticsDataApiSettings settings, AnalyticsDiagnosticsRequest request, IList<AnalyticsDiagnosticsCheck> checks)
    {
        var userDimensionNames = GetUserDimensionNames(request);

        try
        {
            RunRealtimeReportResponse response;
            var checkedCustomDimensions = userDimensionNames.Count > 0;

            try
            {
                response = await _reportClient.RunRealtimeReportAsync(settings.CredentialJson,
                    BuildRealtimeRequest(settings.PropertyId, userDimensionNames));
            }
            catch (RpcException ex) when (checkedCustomDimensions && ex.StatusCode == StatusCode.InvalidArgument)
            {
                checkedCustomDimensions = false;
                response = await _reportClient.RunRealtimeReportAsync(settings.CredentialJson,
                    BuildRealtimeRequest(settings.PropertyId, new List<string>()));
            }

            var fallbackNote = !checkedCustomDimensions && userDimensionNames.Count > 0
                ? " Realtime does not support the custom dimensions on this property — checked event stream only."
                : string.Empty;
            var eventCounts = AggregateEventCounts(response.DimensionHeaders, response.Rows);

            if (eventCounts.Count == 0)
            {
                AddCheck(checks, Stages.Realtime, Statuses.Warning, "No events in the last 30 minutes." + fallbackNote);
            }
            else
            {
                AddCheck(checks, Stages.Realtime, Statuses.Passed,
                    $"Events in the last 30 minutes: {FormatEventCounts(eventCounts)}.{fallbackNote}{DescribeMissingRequestedEvents(request, eventCounts)}");
            }
        }
        catch (Exception ex)
        {
            AddCheck(checks, Stages.Realtime, Statuses.Failed, "Realtime report failed.", DescribeError(ex));
        }
    }

    protected virtual async Task CheckProcessedDataAsync(AnalyticsDataApiSettings settings, AnalyticsDiagnosticsRequest request, IList<AnalyticsDiagnosticsCheck> checks)
    {
        try
        {
            var response = await _reportClient.RunReportAsync(settings.CredentialJson, BuildProcessedDataRequest(settings.PropertyId));
            var eventCounts = AggregateEventCounts(response.DimensionHeaders, response.Rows);

            if (eventCounts.Count == 0)
            {
                AddCheck(checks, Stages.ProcessedData, Statuses.Warning,
                    "No processed data yet — GA4 processes events in up to 24–48 hours.");
            }
            else
            {
                AddCheck(checks, Stages.ProcessedData, Statuses.Passed,
                    $"Processed events over the last 7 days: {FormatEventCounts(eventCounts)}.{DescribeMissingRequestedEvents(request, eventCounts)}");
            }
        }
        catch (Exception ex)
        {
            AddCheck(checks, Stages.ProcessedData, Statuses.Failed, "Processed-data report failed.", DescribeError(ex));
        }
    }

    protected virtual CheckCompatibilityRequest BuildCompatibilityRequest(string propertyId, AnalyticsDiagnosticsReportShape shape, IList<string> userDimensionNames)
    {
        var request = new CheckCompatibilityRequest
        {
            Property = $"properties/{propertyId}",
            CompatibilityFilter = Compatibility.Incompatible,
        };

        foreach (var dimensionName in (shape.DimensionNames ?? []).Where(x => !string.IsNullOrEmpty(x)))
        {
            request.Dimensions.Add(new Dimension { Name = MapDimensionName(dimensionName, userDimensionNames) });
        }

        if (!string.IsNullOrEmpty(shape.MetricName))
        {
            request.Metrics.Add(new Metric { Name = shape.MetricName });
        }

        var expressions = new List<FilterExpression>();
        var eventNames = (shape.EventNames ?? []).Where(x => !string.IsNullOrEmpty(x)).ToList();
        if (eventNames.Count > 0)
        {
            expressions.Add(CreateInListExpression(ModuleConstants.Dimensions.EventName, eventNames));
        }

        expressions.AddRange(userDimensionNames
            .Select(x => CreateInListExpression(UserDimensionPrefix + x, new[] { ProbeFilterValue })));

        var dimensionFilter = CombineExpressions(expressions);
        if (dimensionFilter != null)
        {
            request.DimensionFilter = dimensionFilter;
        }

        return request;
    }

    protected virtual RunRealtimeReportRequest BuildRealtimeRequest(string propertyId, IList<string> userDimensionNames)
    {
        var request = new RunRealtimeReportRequest
        {
            Property = $"properties/{propertyId}",
            Limit = LiveDataRowsLimit,
        };

        request.Dimensions.Add(new Dimension { Name = ModuleConstants.Dimensions.EventName });
        foreach (var dimensionName in userDimensionNames)
        {
            request.Dimensions.Add(new Dimension { Name = UserDimensionPrefix + dimensionName });
        }

        request.Metrics.Add(new Metric { Name = EventCountMetric });
        request.MinuteRanges.Add(new MinuteRange { StartMinutesAgo = RealtimeMinutesAgo, EndMinutesAgo = 0 });

        return request;
    }

    protected virtual RunReportRequest BuildProcessedDataRequest(string propertyId)
    {
        var request = new RunReportRequest
        {
            Property = $"properties/{propertyId}",
            Limit = LiveDataRowsLimit,
        };

        request.DateRanges.Add(new DateRange { StartDate = ProcessedDataStartDate, EndDate = ProcessedDataEndDate });
        request.Dimensions.Add(new Dimension { Name = ModuleConstants.Dimensions.EventName });
        request.Metrics.Add(new Metric { Name = EventCountMetric });

        return request;
    }

    protected virtual IList<string> GetIncompatibleFields(CheckCompatibilityResponse response, ISet<string> requestedFieldNames)
    {
        var incompatibleDimensions = response.DimensionCompatibilities
            .Where(x => x.Compatibility == Compatibility.Incompatible)
            .Select(x => x.DimensionMetadata?.ApiName);
        var incompatibleMetrics = response.MetricCompatibilities
            .Where(x => x.Compatibility == Compatibility.Incompatible)
            .Select(x => x.MetricMetadata?.ApiName);

        return incompatibleDimensions
            .Concat(incompatibleMetrics)
            .Where(x => !string.IsNullOrEmpty(x) && requestedFieldNames.Contains(x))
            .ToList();
    }

    protected virtual ISet<string> GetRequestedFieldNames(CheckCompatibilityRequest request)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        result.UnionWith(request.Dimensions.Select(x => x.Name));
        result.UnionWith(request.Metrics.Select(x => x.Name));
        CollectFilterFieldNames(request.DimensionFilter, result);
        return result;
    }

    protected virtual void CollectFilterFieldNames(FilterExpression expression, ISet<string> fieldNames)
    {
        if (expression == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(expression.Filter?.FieldName))
        {
            fieldNames.Add(expression.Filter.FieldName);
        }

        if (expression.AndGroup != null)
        {
            foreach (var child in expression.AndGroup.Expressions)
            {
                CollectFilterFieldNames(child, fieldNames);
            }
        }
    }

    protected virtual IList<KeyValuePair<string, long>> AggregateEventCounts(IEnumerable<DimensionHeader> dimensionHeaders, IEnumerable<Row> rows)
    {
        var eventNameIndex = dimensionHeaders
            .Select((header, index) => (header.Name, Index: index))
            .Where(x => x.Name == ModuleConstants.Dimensions.EventName)
            .Select(x => (int?)x.Index)
            .FirstOrDefault();

        var counts = new Dictionary<string, long>();

        foreach (var row in rows)
        {
            var eventName = eventNameIndex != null && row.DimensionValues.Count > eventNameIndex.Value
                ? row.DimensionValues[eventNameIndex.Value].Value
                : null;
            if (string.IsNullOrEmpty(eventName))
            {
                continue;
            }

            var count = row.MetricValues.Count > 0
                && long.TryParse(row.MetricValues[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
            counts[eventName] = counts.TryGetValue(eventName, out var existing) ? existing + count : count;
        }

        return counts.OrderByDescending(x => x.Value).ToList();
    }

    protected virtual string FormatEventCounts(IList<KeyValuePair<string, long>> eventCounts)
    {
        return string.Join(", ", eventCounts.Select(x => FormattableString.Invariant($"{x.Key}={x.Value}")));
    }

    protected virtual string DescribeMissingRequestedEvents(AnalyticsDiagnosticsRequest request, IList<KeyValuePair<string, long>> eventCounts)
    {
        var missingNames = (request.EventNames ?? [])
            .Where(x => !string.IsNullOrEmpty(x) && eventCounts.All(count => count.Key != x))
            .Distinct()
            .ToList();

        return missingNames.Count > 0 ? $" Requested events not seen: {string.Join(", ", missingNames)}." : string.Empty;
    }

    protected virtual string DescribeCredentialSource(string credentialJson)
    {
        if (string.IsNullOrWhiteSpace(credentialJson))
        {
            return "Application Default Credentials";
        }

        string credentialType = null;
        try
        {
            credentialType = JObject.Parse(credentialJson)[CredentialTypeProperty]?.Value<string>();
        }
        catch (JsonException)
        {
            // Malformed JSON is diagnosed by the credentials stage; the source kind is still reported here.
        }

        return string.IsNullOrEmpty(credentialType) ? "JSON from setting" : $"{credentialType} from setting";
    }

    protected virtual string DescribeError(Exception exception)
    {
        return exception is RpcException rpcException
            ? $"{rpcException.StatusCode}: {rpcException.Status.Detail}"
            : exception.Message;
    }

    protected virtual IList<string> GetUserDimensionNames(AnalyticsDiagnosticsRequest request)
    {
        return (request.UserDimensionNames ?? [])
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();
    }

    protected virtual string MapDimensionName(string dimensionName, IList<string> userDimensionNames)
    {
        return userDimensionNames.Contains(dimensionName) || ModuleConstants.UserDimensions.AllNames.Contains(dimensionName)
            ? UserDimensionPrefix + dimensionName
            : dimensionName;
    }

    protected virtual void SkipRemainingStages(IList<AnalyticsDiagnosticsCheck> checks, string message)
    {
        foreach (var stage in AllStages.Skip(checks.Count))
        {
            AddCheck(checks, stage, Statuses.Skipped, message);
        }
    }

    protected virtual AnalyticsDiagnosticsCheck AddCheck(IList<AnalyticsDiagnosticsCheck> checks, string stage, string status, string message, string detail = null)
    {
        var check = AbstractTypeFactory<AnalyticsDiagnosticsCheck>.TryCreateInstance();
        check.Stage = stage;
        check.Status = status;
        check.Message = message;
        check.Detail = detail;
        checks.Add(check);
        return check;
    }

    private static string GetProjectName(ErrorInfo errorInfo)
    {
        if (errorInfo?.Metadata == null || !errorInfo.Metadata.TryGetValue(ConsumerMetadataKey, out var consumer))
        {
            return null;
        }

        return consumer.StartsWith(ProjectResourcePrefix, StringComparison.Ordinal)
            ? consumer[ProjectResourcePrefix.Length..]
            : consumer;
    }

    private static FilterExpression CombineExpressions(List<FilterExpression> expressions)
    {
        if (expressions.Count == 0)
        {
            return null;
        }

        if (expressions.Count == 1)
        {
            return expressions[0];
        }

        var result = new FilterExpression { AndGroup = new FilterExpressionList() };
        result.AndGroup.Expressions.AddRange(expressions);
        return result;
    }

    private static FilterExpression CreateInListExpression(string fieldName, IEnumerable<string> values)
    {
        var inListFilter = new Filter.Types.InListFilter();
        inListFilter.Values.AddRange(values);

        return new FilterExpression
        {
            Filter = new Filter { FieldName = fieldName, InListFilter = inListFilter },
        };
    }
}
