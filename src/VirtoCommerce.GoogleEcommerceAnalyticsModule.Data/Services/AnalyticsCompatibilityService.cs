using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public class AnalyticsCompatibilityService : IAnalyticsCompatibilityService
{
    private const string SearchTermsReport = "searchTerms";
    private const string BrowsedProductsReport = "browsedProducts";
    private const string ProbeFilterValue = "compatibility-probe";

    private readonly IAnalyticsSettingsResolver _settingsResolver;
    private readonly GoogleAnalyticsDataSource _googleAnalyticsDataSource;
    private readonly IGoogleAnalyticsReportClient _reportClient;

    public AnalyticsCompatibilityService(
        IAnalyticsSettingsResolver settingsResolver,
        GoogleAnalyticsDataSource googleAnalyticsDataSource,
        IGoogleAnalyticsReportClient reportClient)
    {
        _settingsResolver = settingsResolver;
        _googleAnalyticsDataSource = googleAnalyticsDataSource;
        _reportClient = reportClient;
    }

    public virtual async Task<AnalyticsCompatibilityResult> CheckCompatibilityAsync(string storeId)
    {
        var result = new AnalyticsCompatibilityResult();

        try
        {
            var settings = await _settingsResolver.ResolveAsync(storeId);

            if (settings.SampleDataEnabled)
            {
                result.ErrorMessage = "Sample data mode is enabled; the Google Analytics compatibility check does not apply to the sample data source.";
                return result;
            }

            if (!settings.IsGoogleConfigured)
            {
                result.ErrorMessage = "Google Analytics 4 Data API property is not configured for the store.";
                return result;
            }

            result.Available = true;
            result.Reports.Add(await CheckReportAsync(SearchTermsReport, settings,
                _googleAnalyticsDataSource.BuildEventReportRequest(CreateSearchTermsQuery(settings))));
            result.Reports.Add(await CheckReportAsync(BrowsedProductsReport, settings,
                _googleAnalyticsDataSource.BuildItemReportRequest(CreateBrowsedProductsQuery(settings))));
        }
        catch (Exception ex)
        {
            result.Available = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    protected virtual async Task<AnalyticsReportCompatibility> CheckReportAsync(string reportName, AnalyticsDataApiSettings settings, RunReportRequest reportRequest)
    {
        var result = new AnalyticsReportCompatibility { Report = reportName };

        try
        {
            var request = new CheckCompatibilityRequest
            {
                Property = reportRequest.Property,
                CompatibilityFilter = Compatibility.Incompatible,
            };
            request.Dimensions.AddRange(reportRequest.Dimensions);
            request.Metrics.AddRange(reportRequest.Metrics);
            if (reportRequest.DimensionFilter != null)
            {
                request.DimensionFilter = reportRequest.DimensionFilter;
            }

            var response = await _reportClient.CheckCompatibilityAsync(settings.CredentialJson, request);
            var requestedFields = GetRequestedFieldNames(reportRequest);

            result.IncompatibleDimensions = response.DimensionCompatibilities
                .Where(x => x.Compatibility == Compatibility.Incompatible)
                .Select(x => x.DimensionMetadata?.ApiName)
                .Where(x => !string.IsNullOrEmpty(x) && requestedFields.Contains(x))
                .ToList();
            result.IncompatibleMetrics = response.MetricCompatibilities
                .Where(x => x.Compatibility == Compatibility.Incompatible)
                .Select(x => x.MetricMetadata?.ApiName)
                .Where(x => !string.IsNullOrEmpty(x) && requestedFields.Contains(x))
                .ToList();
            result.Compatible = result.IncompatibleDimensions.Count == 0 && result.IncompatibleMetrics.Count == 0;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    protected virtual AnalyticsDataQuery CreateSearchTermsQuery(AnalyticsDataApiSettings settings)
    {
        return new AnalyticsDataQuery
        {
            PropertyId = settings.PropertyId,
            CredentialJson = settings.CredentialJson,
            EventNames = new List<string> { ModuleConstants.EventNames.Search, ModuleConstants.EventNames.ViewSearchResults },
            DimensionNames = new List<string> { ModuleConstants.Dimensions.SearchTerm },
            DimensionFilters = CreateUserDimensionFilters(),
            Take = 1,
        };
    }

    protected virtual AnalyticsDataQuery CreateBrowsedProductsQuery(AnalyticsDataApiSettings settings)
    {
        return new AnalyticsDataQuery
        {
            PropertyId = settings.PropertyId,
            CredentialJson = settings.CredentialJson,
            EventNames = new List<string> { ModuleConstants.EventNames.ViewItem },
            DimensionNames = new List<string> { ModuleConstants.Dimensions.ItemId, ModuleConstants.Dimensions.ItemName },
            DimensionFilters = CreateUserDimensionFilters(),
            Take = 1,
        };
    }

    protected virtual IList<AnalyticsDimensionFilter> CreateUserDimensionFilters()
    {
        var sessionKindFilter = AbstractTypeFactory<AnalyticsDimensionFilter>.TryCreateInstance();
        sessionKindFilter.DimensionName = ModuleConstants.UserDimensions.SessionKind;
        sessionKindFilter.Values = new List<string> { ModuleConstants.SessionKinds.Self };

        var organizationFilter = AbstractTypeFactory<AnalyticsDimensionFilter>.TryCreateInstance();
        organizationFilter.DimensionName = ModuleConstants.UserDimensions.OrganizationId;
        organizationFilter.Values = new List<string> { ProbeFilterValue };

        return new List<AnalyticsDimensionFilter> { sessionKindFilter, organizationFilter };
    }

    protected virtual ISet<string> GetRequestedFieldNames(RunReportRequest request)
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
}
