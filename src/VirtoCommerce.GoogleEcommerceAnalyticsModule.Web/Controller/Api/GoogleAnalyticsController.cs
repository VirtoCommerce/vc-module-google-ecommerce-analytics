using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Controller.Api
{
    [Route("api/googleanalytics")]
    public class GoogleAnalyticsController
    {
        private readonly IGoogleAnalyticsSettingsManager _settings;

        public GoogleAnalyticsController(IGoogleAnalyticsSettingsManager settings)
        {
            _settings = settings;
        }

        [HttpGet]
        [Route("{storeId}")]
        public Task<GoogleAnalyticsSettings> GetStoreSettings(string storeId)
        {
            return _settings.GetAsync(storeId);
        }
    }
}
