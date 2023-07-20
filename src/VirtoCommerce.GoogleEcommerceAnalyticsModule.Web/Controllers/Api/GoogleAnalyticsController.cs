using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Settings;
using GoogleSettings = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Controllers.Api
{
    [Route("api/googleanalytics")]
    public class GoogleAnalyticsController : Controller
    {
        private readonly IGoogleAnalyticsSettingsManager _settings;
        private readonly ISettingsManager _settingsManager;

        public GoogleAnalyticsController(IGoogleAnalyticsSettingsManager settings, ISettingsManager settingsManager)
        {
            _settings = settings;
            _settingsManager = settingsManager;
        }

        [HttpGet]
        [Route("{storeId}")]
        public Task<GoogleAnalyticsSettings> GetStoreSettings(string storeId)
        {
            return _settings.GetAsync(storeId);
        }

        [HttpGet]
        [Route("redirect")]
        [Authorize(ModuleConstants.Security.Permissions.Access)]
        public ActionResult Redirect()
        {
            var redirectUrl = _settingsManager.GetValue<string>(GoogleSettings.GoogleAnalyticsUrl);

            if (string.IsNullOrEmpty(redirectUrl))
            {
                return NotFound("GoogleAnalyticsUrl is not configured in Platform Settings");
            }

            return Redirect(redirectUrl);
        }
    }
}
