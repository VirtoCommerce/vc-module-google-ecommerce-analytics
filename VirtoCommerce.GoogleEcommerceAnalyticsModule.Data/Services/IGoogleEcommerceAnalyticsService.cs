using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
    public interface IGoogleEcommerceAnalyticsService
    {
        StoreSettings GetSettingsByStoreId(string storeId);
        void AddOrUpdate(StoreSettings settings);
    }
}
