using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Repositories
{
    public interface IGoogleEcommerceAnalyticsRepository : IRepository
    {
        StoreSettings GetSettingsByStoreId(string storeId);
    }
}
