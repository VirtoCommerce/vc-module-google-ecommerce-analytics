using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Tests;

/// <summary>
/// What each action declares about who may call it. The platform sets DefaultPolicy but no FallbackPolicy, so an
/// action without an [Authorize] is reachable anonymously - which is right for the tag settings the storefront
/// renders on every page, and wrong for diagnostics, which names the property, the credential kind and the Google
/// errors behind a failure. The difference is asserted here rather than assumed.
/// </summary>
public class GoogleAnalyticsControllerTests
{
    [Fact]
    public void RunDiagnostics_DeclaresTheAccessPermission()
    {
        var attribute = GetAuthorizeAttributes(nameof(GoogleAnalyticsController.RunDiagnostics)).Single();

        Assert.Equal(ModuleConstants.Security.Permissions.Access, attribute.Policy);
    }

    [Fact]
    public void Redirect_DeclaresTheAccessPermission()
    {
        var attribute = GetAuthorizeAttributes(nameof(GoogleAnalyticsController.Redirect)).Single();

        Assert.Equal(ModuleConstants.Security.Permissions.Access, attribute.Policy);
    }

    // The storefront reads it before anyone signs in, and it carries only the public tag identifiers.
    [Fact]
    public void GetStoreSettings_IsReachableAnonymously()
    {
        Assert.Empty(GetAuthorizeAttributes(nameof(GoogleAnalyticsController.GetStoreSettings)));
        Assert.Empty(typeof(GoogleAnalyticsController).GetCustomAttributes<AuthorizeAttribute>());
    }

    [Fact]
    public async Task RunDiagnostics_PassesTheStoreAndRequestThrough_AndReturnsTheReport()
    {
        var report = new AnalyticsDiagnosticsResult
        {
            Checks = [new AnalyticsDiagnosticsCheck { Stage = ModuleConstants.DiagnosticsStages.Configuration }],
        };
        var diagnosticsService = new FakeDiagnosticsService(report);
        var controller = new GoogleAnalyticsController(new FakeSettingsManager(), null, diagnosticsService);
        var request = new AnalyticsDiagnosticsRequest { EventNames = [ModuleConstants.EventNames.Search] };

        var response = await controller.RunDiagnostics("B2B-store", request);

        Assert.Same(report, Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal("B2B-store", diagnosticsService.StoreId);
        Assert.Same(request, diagnosticsService.Request);
    }

    private static AuthorizeAttribute[] GetAuthorizeAttributes(string actionName)
    {
        // DeclaredOnly: the Redirect action shares its name with ControllerBase.Redirect(string).
        return typeof(GoogleAnalyticsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(x => x.Name == actionName)
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToArray();
    }

    private sealed class FakeDiagnosticsService : IAnalyticsDiagnosticsService
    {
        private readonly AnalyticsDiagnosticsResult _report;

        public FakeDiagnosticsService(AnalyticsDiagnosticsResult report)
        {
            _report = report;
        }

        public string StoreId { get; private set; }

        public AnalyticsDiagnosticsRequest Request { get; private set; }

        public Task<AnalyticsDiagnosticsResult> RunAsync(string storeId, AnalyticsDiagnosticsRequest request)
        {
            StoreId = storeId;
            Request = request;

            return Task.FromResult(_report);
        }
    }

    private sealed class FakeSettingsManager : IGoogleAnalyticsSettingsManager
    {
        public Task<GoogleAnalyticsSettings> GetAsync(string storeId)
        {
            return Task.FromResult(new GoogleAnalyticsSettings());
        }
    }
}
