using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Security
{
    /// <summary>
    /// Restricted to permission within selected stores
    /// </summary>
    public class GoogleEcommerceAnalyticsStoreScope : PermissionScope
    {
        public override bool IsScopeAvailableForPermission(string permission)
        {
            return permission == GoogleEcommerceAnalyticsPredefinedPermissions.Read
                      || permission == GoogleEcommerceAnalyticsPredefinedPermissions.Update
                      || permission == GoogleEcommerceAnalyticsPredefinedPermissions.Create
                      || permission == GoogleEcommerceAnalyticsPredefinedPermissions.Delete;
        }

        public override IEnumerable<string> GetEntityScopeStrings(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            var contentScopeObj = obj as GoogleEcommerceAnalyticsScopeObject;
            if (contentScopeObj != null)
                return new[] { Type + ":" + contentScopeObj.StoreId };

            return Enumerable.Empty<string>(); ;
        }
    }
}