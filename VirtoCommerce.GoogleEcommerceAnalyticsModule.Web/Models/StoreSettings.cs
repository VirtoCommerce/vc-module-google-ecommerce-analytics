using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Models
{
    public class StoreSettings
    {
        public string Id { get; set; }

        public string StoreId { get; set; }

        public string GoogleTagManagerId { get; set; }

        public bool IsActive { get; set; }
    }
}