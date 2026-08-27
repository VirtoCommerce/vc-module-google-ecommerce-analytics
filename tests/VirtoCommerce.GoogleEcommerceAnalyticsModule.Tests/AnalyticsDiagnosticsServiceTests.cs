using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.Platform.Core.Caching;
using Xunit;
using ErrorInfo = Google.Rpc.ErrorInfo;
using GaMetadata = Google.Analytics.Data.V1Beta.Metadata;
using GrpcMetadata = Grpc.Core.Metadata;
using GrpcStatus = Grpc.Core.Status;
using RpcStatus = Google.Rpc.Status;
using Stages = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.DiagnosticsStages;
using Statuses = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.DiagnosticsStatuses;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

public class AnalyticsDiagnosticsServiceTests
{
    private const string StoreId = "test-store";
    private const string PropertyId = "123456";

    private static readonly string[] StageOrder =
    {
        Stages.Configuration,
        Stages.Credentials,
        Stages.ApiAccess,
        Stages.CustomDimensions,
        Stages.ReportCompatibility,
        Stages.Realtime,
        Stages.ProcessedData,
    };

    private static readonly string[] ExpectedReportDimensions = { "eventName", "dateHour", "searchTerm" };
    private static readonly string[] ExpectedFilteredDimensions = { "eventName", "customUser:session_kind", "customUser:organization_id" };
    private static readonly string[] ExpectedEventNameFilterValues = { "search", "view_search_results" };
    private static readonly string[] ExpectedEventNameDimensionOnly = { "eventName" };

    private readonly Mock<IAnalyticsSettingsResolver> _settingsResolverMock = new();
    private readonly Mock<IGoogleAnalyticsReportClient> _reportClientMock = new();
    private readonly List<CheckCompatibilityRequest> _capturedCompatibilityRequests = new();
    private readonly List<RunRealtimeReportRequest> _capturedRealtimeRequests = new();
    private readonly List<RunReportRequest> _capturedReportRequests = new();

    [Fact]
    public async Task RunAsync_HappyPath_AllStagesPassInStableOrder()
    {
        SetupHappyGooglePath();
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        AssertStageOrder(result);
        Assert.All(result.Checks, x => Assert.Equal(Statuses.Passed, x.Status));

        var configuration = GetCheck(result, Stages.Configuration);
        Assert.Contains(PropertyId, configuration.Message);
        Assert.Contains("service_account from setting", configuration.Message);
        Assert.DoesNotContain("private", configuration.Message);

        Assert.Contains("session_kind", GetCheck(result, Stages.CustomDimensions).Message);
        Assert.Contains("searchTerms", GetCheck(result, Stages.ReportCompatibility).Message);

        var realtime = GetCheck(result, Stages.Realtime);
        Assert.Contains("search=5", realtime.Message);
        Assert.Contains("Requested events not seen: view_item", realtime.Message);
        Assert.DoesNotContain("checked event stream only", realtime.Message);

        var processedData = GetCheck(result, Stages.ProcessedData);
        Assert.Contains("search=10", processedData.Message);
        Assert.Contains("view_item=4", processedData.Message);
        Assert.DoesNotContain("Requested events not seen", processedData.Message);
    }

    [Fact]
    public async Task RunAsync_NullRequest_RunsWithDefaults()
    {
        SetupHappyGooglePath();
        var service = CreateService();

        var result = await service.RunAsync(StoreId, null);

        AssertStageOrder(result);
        Assert.Equal(Statuses.Skipped, GetCheck(result, Stages.CustomDimensions).Status);
        Assert.Equal(Statuses.Skipped, GetCheck(result, Stages.ReportCompatibility).Status);
        Assert.Equal(Statuses.Passed, GetCheck(result, Stages.Realtime).Status);
        Assert.Equal(Statuses.Passed, GetCheck(result, Stages.ProcessedData).Status);
        Assert.Single(Assert.Single(_capturedRealtimeRequests).Dimensions);
    }

    [Fact]
    public async Task RunAsync_SampleMode_WarnsAndSkipsGoogleStages()
    {
        SetupSettings(new AnalyticsDataApiSettings { PropertyId = PropertyId, SampleDataEnabled = true });
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        AssertStageOrder(result);
        var configuration = GetCheck(result, Stages.Configuration);
        Assert.Equal(Statuses.Warning, configuration.Status);
        Assert.Contains("Sample data mode", configuration.Message);
        Assert.All(result.Checks.Skip(1), x =>
        {
            Assert.Equal(Statuses.Skipped, x.Status);
            Assert.Contains("sample data mode", x.Message);
        });
        _reportClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunAsync_NoPropertyId_FailsConfigurationAndSkipsRest()
    {
        SetupSettings(new AnalyticsDataApiSettings());
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        AssertStageOrder(result);
        var configuration = GetCheck(result, Stages.Configuration);
        Assert.Equal(Statuses.Failed, configuration.Status);
        Assert.Contains("PropertyId", configuration.Message);
        Assert.All(result.Checks.Skip(1), x => Assert.Equal(Statuses.Skipped, x.Status));
        _reportClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunAsync_ResolverThrows_FailsConfigurationWithDetail()
    {
        _settingsResolverMock
            .Setup(x => x.ResolveAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        AssertStageOrder(result);
        var configuration = GetCheck(result, Stages.Configuration);
        Assert.Equal(Statuses.Failed, configuration.Status);
        Assert.Equal("boom", configuration.Detail);
        Assert.All(result.Checks.Skip(1), x => Assert.Equal(Statuses.Skipped, x.Status));
    }

    [Fact]
    public async Task RunAsync_AdcCredential_ReportsAdcSource()
    {
        SetupHappyGooglePath();
        SetupSettings(new AnalyticsDataApiSettings { PropertyId = PropertyId });
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        Assert.Contains("Application Default Credentials", GetCheck(result, Stages.Configuration).Message);
    }

    [Fact]
    public async Task RunAsync_UnparseableCredentialJson_ReportsSourceWithoutContent()
    {
        SetupHappyGooglePath();
        SetupSettings(new AnalyticsDataApiSettings { PropertyId = PropertyId, CredentialJson = "{secret-not-json" });
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        var configuration = GetCheck(result, Stages.Configuration);
        Assert.Contains("JSON from setting", configuration.Message);
        Assert.DoesNotContain("secret", configuration.Message);
    }

    [Fact]
    public async Task Credentials_AdcNotFound_FailsAndSkipsRest()
    {
        SetupSettings(new AnalyticsDataApiSettings { PropertyId = PropertyId });
        _reportClientMock
            .Setup(x => x.ValidateCredentialAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("The Application Default Credentials are not available."));
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        AssertStageOrder(result);
        var credentials = GetCheck(result, Stages.Credentials);
        Assert.Equal(Statuses.Failed, credentials.Status);
        Assert.Contains("Application Default Credentials", credentials.Message);
        Assert.All(result.Checks.Skip(2), x => Assert.Equal(Statuses.Skipped, x.Status));
        _reportClientMock.Verify(x => x.GetMetadataAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Credentials_MalformedConfiguredJson_MessagePointsAtSetting()
    {
        SetupGoogleSettings();
        _reportClientMock
            .Setup(x => x.ValidateCredentialAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Error creating credential from JSON."));
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        var credentials = GetCheck(result, Stages.Credentials);
        Assert.Equal(Statuses.Failed, credentials.Status);
        Assert.Contains("GoogleAnalytics4.DataApi.ServiceAccountJson", credentials.Message);
        Assert.Equal("Error creating credential from JSON.", credentials.Detail);
    }

    [Fact]
    public async Task ApiAccess_ScopeInsufficient_MapsToReadonlyScopeMessage()
    {
        var apiAccess = await RunApiAccessFailureAsync(CreateRpcException(
            StatusCode.PermissionDenied, "Request had insufficient authentication scopes.", "ACCESS_TOKEN_SCOPE_INSUFFICIENT"));

        Assert.Contains("https://www.googleapis.com/auth/analytics.readonly", apiAccess.Message);
    }

    [Fact]
    public async Task ApiAccess_ServiceDisabled_MapsToEnableApiMessageWithProject()
    {
        var apiAccess = await RunApiAccessFailureAsync(CreateRpcException(
            StatusCode.PermissionDenied,
            "Google Analytics Data API has not been used in project 98765 before or it is disabled.",
            "SERVICE_DISABLED",
            new Dictionary<string, string> { ["consumer"] = "projects/98765" }));

        Assert.Contains("analyticsdata.googleapis.com", apiAccess.Message);
        Assert.Contains("project 98765", apiAccess.Message);
    }

    [Fact]
    public async Task ApiAccess_UserProjectDenied_MapsToQuotaProjectMessage()
    {
        var apiAccess = await RunApiAccessFailureAsync(CreateRpcException(
            StatusCode.PermissionDenied, "Permission denied on the consumer project.", "USER_PROJECT_DENIED"));

        Assert.Contains("quota project", apiAccess.Message);
    }

    [Fact]
    public async Task ApiAccess_QuotaProjectInMessage_MapsToQuotaProjectMessage()
    {
        var apiAccess = await RunApiAccessFailureAsync(CreateRpcException(
            StatusCode.PermissionDenied,
            "Your application is authenticating by using local Application Default Credentials. The analyticsdata.googleapis.com API requires a quota project, which is not set by default."));

        Assert.Contains("quota project", apiAccess.Message);
    }

    [Fact]
    public async Task ApiAccess_PermissionDenied_MapsToGrantViewerMessage()
    {
        var apiAccess = await RunApiAccessFailureAsync(CreateRpcException(
            StatusCode.PermissionDenied, "User does not have sufficient permissions for this property."));

        Assert.Contains("Viewer", apiAccess.Message);
        Assert.Contains(PropertyId, apiAccess.Message);
    }

    [Fact]
    public async Task ApiAccess_NotFound_MapsToWrongPropertyIdMessage()
    {
        var apiAccess = await RunApiAccessFailureAsync(CreateRpcException(
            StatusCode.NotFound, "Could not find entity."));

        Assert.Contains("was not found", apiAccess.Message);
        Assert.Contains(PropertyId, apiAccess.Message);
    }

    [Fact]
    public async Task ApiAccess_UnmappedError_FailsWithRawDetail()
    {
        var apiAccess = await RunApiAccessFailureAsync(CreateRpcException(StatusCode.Internal, "backend error"));

        Assert.Equal("Google Analytics Data API call failed.", apiAccess.Message);
        Assert.Contains("Internal", apiAccess.Detail);
        Assert.Contains("backend error", apiAccess.Detail);
    }

    [Fact]
    public async Task CustomDimensions_Missing_FailsNamingThemAndLaterStagesStillRun()
    {
        SetupHappyGooglePath();
        SetupMetadata("customUser:session_kind");
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        var customDimensions = GetCheck(result, Stages.CustomDimensions);
        Assert.Equal(Statuses.Failed, customDimensions.Status);
        Assert.Contains("organization_id", customDimensions.Message);
        Assert.DoesNotContain("session_kind,", customDimensions.Message);
        Assert.Contains("Custom definitions", customDimensions.Message);
        Assert.Contains("not retroactive", customDimensions.Message);
        Assert.Equal(Statuses.Passed, GetCheck(result, Stages.ReportCompatibility).Status);
        Assert.Equal(Statuses.Passed, GetCheck(result, Stages.Realtime).Status);
    }

    [Fact]
    public async Task CustomDimensions_NoneRequested_SkippedAndRealtimeOmitsCustomDimensions()
    {
        SetupHappyGooglePath();
        var request = CreateRequest();
        request.UserDimensionNames.Clear();
        var service = CreateService();

        var result = await service.RunAsync(StoreId, request);

        Assert.Equal(Statuses.Skipped, GetCheck(result, Stages.CustomDimensions).Status);
        Assert.Single(Assert.Single(_capturedRealtimeRequests).Dimensions);
    }

    [Fact]
    public async Task ReportCompatibility_BuildsRequestFromShapeAndUserDimensions()
    {
        SetupHappyGooglePath();
        var service = CreateService();

        await service.RunAsync(StoreId, CreateRequest());

        var request = Assert.Single(_capturedCompatibilityRequests);
        Assert.Equal($"properties/{PropertyId}", request.Property);
        Assert.Equal(Compatibility.Incompatible, request.CompatibilityFilter);
        Assert.Equal(ExpectedReportDimensions, request.Dimensions.Select(x => x.Name));
        Assert.Equal("eventCount", Assert.Single(request.Metrics).Name);
        Assert.Equal(
            ExpectedFilteredDimensions,
            request.DimensionFilter.AndGroup.Expressions.Select(x => x.Filter.FieldName));
        Assert.Equal(
            ExpectedEventNameFilterValues,
            request.DimensionFilter.AndGroup.Expressions[0].Filter.InListFilter.Values);
    }

    [Fact]
    public async Task ReportCompatibility_IncompatibleFieldsIntersectedWithRequested()
    {
        SetupHappyGooglePath();
        var response = new CheckCompatibilityResponse();
        response.DimensionCompatibilities.Add(new DimensionCompatibility
        {
            Compatibility = Compatibility.Incompatible,
            DimensionMetadata = new DimensionMetadata { ApiName = "customUser:organization_id" },
        });
        response.DimensionCompatibilities.Add(new DimensionCompatibility
        {
            Compatibility = Compatibility.Incompatible,
            DimensionMetadata = new DimensionMetadata { ApiName = "unrelatedDimension" },
        });
        response.MetricCompatibilities.Add(new MetricCompatibility
        {
            Compatibility = Compatibility.Incompatible,
            MetricMetadata = new MetricMetadata { ApiName = "itemsViewed" },
        });
        SetupCompatibility(response);
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        var reportCompatibility = GetCheck(result, Stages.ReportCompatibility);
        Assert.Equal(Statuses.Failed, reportCompatibility.Status);
        Assert.Contains("searchTerms", reportCompatibility.Message);
        Assert.Contains("customUser:organization_id", reportCompatibility.Message);
        Assert.DoesNotContain("unrelatedDimension", reportCompatibility.Message);
        Assert.DoesNotContain("itemsViewed", reportCompatibility.Message);
    }

    [Fact]
    public async Task ReportCompatibility_ClientThrows_FailsStageButLiveDataStillRuns()
    {
        SetupHappyGooglePath();
        _reportClientMock
            .Setup(x => x.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CheckCompatibilityRequest>()))
            .ThrowsAsync(new InvalidOperationException("credentials missing"));
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        var reportCompatibility = GetCheck(result, Stages.ReportCompatibility);
        Assert.Equal(Statuses.Failed, reportCompatibility.Status);
        Assert.Equal("credentials missing", reportCompatibility.Detail);
        Assert.Equal(Statuses.Passed, GetCheck(result, Stages.Realtime).Status);
        Assert.Equal(Statuses.Passed, GetCheck(result, Stages.ProcessedData).Status);
    }

    [Fact]
    public async Task ReportCompatibility_NoShapes_Skipped()
    {
        SetupHappyGooglePath();
        var request = CreateRequest();
        request.Reports.Clear();
        var service = CreateService();

        var result = await service.RunAsync(StoreId, request);

        Assert.Equal(Statuses.Skipped, GetCheck(result, Stages.ReportCompatibility).Status);
        _reportClientMock.Verify(x => x.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CheckCompatibilityRequest>()), Times.Never);
    }

    [Fact]
    public async Task Realtime_CustomDimensionsRejected_RetriesEventNameOnly()
    {
        SetupHappyGooglePath();
        _reportClientMock
            .Setup(x => x.RunRealtimeReportAsync(It.IsAny<string>(), It.Is<RunRealtimeReportRequest>(r => r.Dimensions.Count > 1)))
            .Callback((string _, RunRealtimeReportRequest request) => _capturedRealtimeRequests.Add(request))
            .ThrowsAsync(CreateRpcException(StatusCode.InvalidArgument, "Field customUser:session_kind is not a valid dimension."));
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        var realtime = GetCheck(result, Stages.Realtime);
        Assert.Equal(Statuses.Passed, realtime.Status);
        Assert.Contains("checked event stream only", realtime.Message);
        Assert.Contains("search=5", realtime.Message);
        Assert.Equal(2, _capturedRealtimeRequests.Count);
        Assert.Equal(ExpectedFilteredDimensions, _capturedRealtimeRequests[0].Dimensions.Select(x => x.Name));
        Assert.Equal(ExpectedEventNameDimensionOnly, _capturedRealtimeRequests[1].Dimensions.Select(x => x.Name));
    }

    [Fact]
    public async Task LiveData_ZeroRows_WarningNotFailed()
    {
        SetupHappyGooglePath();
        SetupRealtime(new RunRealtimeReportResponse());
        SetupReport(new RunReportResponse());
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        var realtime = GetCheck(result, Stages.Realtime);
        Assert.Equal(Statuses.Warning, realtime.Status);
        Assert.Contains("No events in the last 30 minutes", realtime.Message);

        var processedData = GetCheck(result, Stages.ProcessedData);
        Assert.Equal(Statuses.Warning, processedData.Status);
        Assert.Contains("24–48", processedData.Message);
    }

    [Fact]
    public async Task LiveData_Disabled_SkipsBothChecks()
    {
        SetupHappyGooglePath();
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest(includeLiveData: false));

        AssertStageOrder(result);
        Assert.Equal(Statuses.Skipped, GetCheck(result, Stages.Realtime).Status);
        Assert.Equal(Statuses.Skipped, GetCheck(result, Stages.ProcessedData).Status);
        _reportClientMock.Verify(x => x.RunRealtimeReportAsync(It.IsAny<string>(), It.IsAny<RunRealtimeReportRequest>()), Times.Never);
        _reportClientMock.Verify(x => x.RunReportAsync(It.IsAny<string>(), It.IsAny<RunReportRequest>()), Times.Never);
    }

    [Fact]
    public async Task Realtime_HardFailure_FailsRealtimeOnlyProcessedStillRuns()
    {
        SetupHappyGooglePath();
        _reportClientMock
            .Setup(x => x.RunRealtimeReportAsync(It.IsAny<string>(), It.IsAny<RunRealtimeReportRequest>()))
            .ThrowsAsync(CreateRpcException(StatusCode.Unavailable, "try again later"));
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        var realtime = GetCheck(result, Stages.Realtime);
        Assert.Equal(Statuses.Failed, realtime.Status);
        Assert.Contains("Unavailable", realtime.Detail);
        Assert.Equal(Statuses.Passed, GetCheck(result, Stages.ProcessedData).Status);
    }

    [Fact]
    public void Constructor_BypassesAnalyticsCacheAndService()
    {
        var parameterTypes = typeof(AnalyticsDiagnosticsService)
            .GetConstructors()
            .SelectMany(x => x.GetParameters())
            .Select(x => x.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(IPlatformMemoryCache), parameterTypes);
        Assert.DoesNotContain(typeof(IAnalyticsService), parameterTypes);
        Assert.DoesNotContain(typeof(AnalyticsService), parameterTypes);
    }

    private async Task<AnalyticsDiagnosticsCheck> RunApiAccessFailureAsync(RpcException exception)
    {
        SetupGoogleSettings();
        _reportClientMock
            .Setup(x => x.GetMetadataAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(exception);
        var service = CreateService();

        var result = await service.RunAsync(StoreId, CreateRequest());

        AssertStageOrder(result);
        var apiAccess = GetCheck(result, Stages.ApiAccess);
        Assert.Equal(Statuses.Failed, apiAccess.Status);
        Assert.All(result.Checks.Skip(3), x => Assert.Equal(Statuses.Skipped, x.Status));
        return apiAccess;
    }

    private AnalyticsDiagnosticsService CreateService()
    {
        return new AnalyticsDiagnosticsService(_settingsResolverMock.Object, _reportClientMock.Object);
    }

    private void SetupHappyGooglePath()
    {
        SetupGoogleSettings();
        SetupMetadata("customUser:session_kind", "customUser:organization_id");
        SetupCompatibility(new CheckCompatibilityResponse());
        SetupRealtime(CreateRealtimeResponse(("search", "2"), ("search", "3")));
        SetupReport(CreateReportResponse(("search", "10"), ("view_item", "4")));
    }

    private void SetupGoogleSettings()
    {
        SetupSettings(new AnalyticsDataApiSettings { PropertyId = PropertyId, CredentialJson = """{"type":"service_account"}""" });
    }

    private void SetupSettings(AnalyticsDataApiSettings settings)
    {
        _settingsResolverMock
            .Setup(x => x.ResolveAsync(StoreId))
            .ReturnsAsync(settings);
    }

    private void SetupMetadata(params string[] customDimensionApiNames)
    {
        var metadata = new GaMetadata();
        foreach (var apiName in customDimensionApiNames)
        {
            metadata.Dimensions.Add(new DimensionMetadata { ApiName = apiName, CustomDefinition = true });
        }

        _reportClientMock
            .Setup(x => x.GetMetadataAsync(It.IsAny<string>(), PropertyId))
            .ReturnsAsync(metadata);
    }

    private void SetupCompatibility(CheckCompatibilityResponse response)
    {
        _reportClientMock
            .Setup(x => x.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CheckCompatibilityRequest>()))
            .Callback((string _, CheckCompatibilityRequest request) => _capturedCompatibilityRequests.Add(request))
            .ReturnsAsync(response);
    }

    private void SetupRealtime(RunRealtimeReportResponse response)
    {
        _reportClientMock
            .Setup(x => x.RunRealtimeReportAsync(It.IsAny<string>(), It.IsAny<RunRealtimeReportRequest>()))
            .Callback((string _, RunRealtimeReportRequest request) => _capturedRealtimeRequests.Add(request))
            .ReturnsAsync(response);
    }

    private void SetupReport(RunReportResponse response)
    {
        _reportClientMock
            .Setup(x => x.RunReportAsync(It.IsAny<string>(), It.IsAny<RunReportRequest>()))
            .Callback((string _, RunReportRequest request) => _capturedReportRequests.Add(request))
            .ReturnsAsync(response);
    }

    private static AnalyticsDiagnosticsRequest CreateRequest(bool includeLiveData = true)
    {
        return new AnalyticsDiagnosticsRequest
        {
            UserDimensionNames = new List<string> { "session_kind", "organization_id" },
            EventNames = new List<string> { "search", "view_item" },
            Reports = new List<AnalyticsDiagnosticsReportShape>
            {
                new()
                {
                    Name = "searchTerms",
                    DimensionNames = new List<string> { "eventName", "dateHour", "searchTerm" },
                    MetricName = "eventCount",
                    EventNames = new List<string> { "search", "view_search_results" },
                },
            },
            IncludeLiveData = includeLiveData,
        };
    }

    private static RunRealtimeReportResponse CreateRealtimeResponse(params (string EventName, string Count)[] rows)
    {
        var response = new RunRealtimeReportResponse();
        response.DimensionHeaders.Add(new DimensionHeader { Name = "eventName" });
        foreach (var (eventName, count) in rows)
        {
            response.Rows.Add(CreateRow(eventName, count));
        }

        return response;
    }

    private static RunReportResponse CreateReportResponse(params (string EventName, string Count)[] rows)
    {
        var response = new RunReportResponse();
        response.DimensionHeaders.Add(new DimensionHeader { Name = "eventName" });
        foreach (var (eventName, count) in rows)
        {
            response.Rows.Add(CreateRow(eventName, count));
        }

        return response;
    }

    private static Row CreateRow(string eventName, string count)
    {
        var row = new Row();
        row.DimensionValues.Add(new DimensionValue { Value = eventName });
        row.MetricValues.Add(new MetricValue { Value = count });
        return row;
    }

    private static RpcException CreateRpcException(StatusCode statusCode, string message, string reason = null, IDictionary<string, string> errorMetadata = null)
    {
        var status = new GrpcStatus(statusCode, message);
        if (reason == null)
        {
            return new RpcException(status);
        }

        var errorInfo = new ErrorInfo { Reason = reason, Domain = "googleapis.com" };
        if (errorMetadata != null)
        {
            foreach (var (key, value) in errorMetadata)
            {
                errorInfo.Metadata.Add(key, value);
            }
        }

        var rpcStatus = new RpcStatus { Code = (int)statusCode, Message = message, Details = { Any.Pack(errorInfo) } };
        var trailers = new GrpcMetadata { { "grpc-status-details-bin", rpcStatus.ToByteArray() } };
        return new RpcException(status, trailers);
    }

    private static AnalyticsDiagnosticsCheck GetCheck(AnalyticsDiagnosticsResult result, string stage)
    {
        return result.Checks.Single(x => x.Stage == stage);
    }

    private static void AssertStageOrder(AnalyticsDiagnosticsResult result)
    {
        Assert.Equal(StageOrder, result.Checks.Select(x => x.Stage));
    }
}
