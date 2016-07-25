using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models
{
    public class StoreSettings : AuditableEntity
    {
        [StringLength(128)]
        public string StoreId { get; set; }

        [StringLength(128)]
        public string GoogleTagManagerId { get; set; }

        public bool IsActive { get; set; }
    }
}
