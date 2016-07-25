using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Omu.ValueInjecter;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Data.Common.ConventionInjections;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Converters
{
    public static class StoreSettingsConverter
    {
        /// <summary>
        /// Patch changes
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        public static void Patch(this StoreSettings source, StoreSettings target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            var patchInjectionPolicy = new PatchInjection<StoreSettings>(x => x.IsActive, x => x.StoreId, x => x.GoogleTagManagerId);

            target.InjectFrom(patchInjectionPolicy, source);
        }
    }
}
