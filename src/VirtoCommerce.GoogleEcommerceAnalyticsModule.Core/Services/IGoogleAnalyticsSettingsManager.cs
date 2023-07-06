using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services
{
    public interface IGoogleAnalyticsSettingsManager
    {
        Task<GoogleAnalyticsSettings> GetAsync(string storeId);
    }
}
