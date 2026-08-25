using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public interface IAnalyticsCompatibilityService
{
    Task<AnalyticsCompatibilityResult> CheckCompatibilityAsync(string storeId);
}
