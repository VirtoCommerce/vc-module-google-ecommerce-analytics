using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Converters;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Security;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Web.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Controllers.Api
{
    [RoutePrefix("api/ga/{storeId}")]
    public class StoreSettingsController : ContentBaseController
    {
        private readonly IGoogleEcommerceAnalyticsService _googleEcommerceAnalyticsService;

        public StoreSettingsController(IGoogleEcommerceAnalyticsService googleEcommerceAnalyticsService, ISecurityService securityService, IPermissionScopeService permissionScopeService)
            : base(securityService, permissionScopeService)
        {
            if (googleEcommerceAnalyticsService == null)
                throw new ArgumentNullException("googleEcommerceAnalyticsService");

            _googleEcommerceAnalyticsService = googleEcommerceAnalyticsService;
        }

        /// <summary>
        /// Get store settings by store id
        /// </summary>
        /// <param name="storeId">Store id</param>
		[HttpGet]
        [ResponseType(typeof(StoreSettings))]
        [ClientCache(Duration = 60)]
        [Route("settings")]
        public IHttpActionResult GetSettings(string storeId)
        {
            base.CheckCurrentUserHasPermissionForObjects(GoogleEcommerceAnalyticsPredefinedPermissions.Read, new GoogleEcommerceAnalyticsScopeObject { StoreId = storeId });

            var settings = _googleEcommerceAnalyticsService.GetSettingsByStoreId(storeId);
            if (settings != null)
            {
                return this.Ok(settings.ToWebModel());
            }
            return StatusCode(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Update store settings
        /// </summary>
        /// <param name="settings">Store settings</param>
        [HttpPost]
        [ResponseType(typeof(void))]
        [Route("settings")]
        public IHttpActionResult Update(StoreSettings settings)
        {
            base.CheckCurrentUserHasPermissionForObjects(GoogleEcommerceAnalyticsPredefinedPermissions.Update, new GoogleEcommerceAnalyticsScopeObject { StoreId = settings.StoreId });

            _googleEcommerceAnalyticsService.AddOrUpdate(settings.ToCoreModel());
            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}