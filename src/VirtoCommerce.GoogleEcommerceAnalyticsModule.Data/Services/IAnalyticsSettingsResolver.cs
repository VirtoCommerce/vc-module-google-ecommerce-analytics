using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

public interface IAnalyticsSettingsResolver
{
    Task<AnalyticsDataApiSettings> ResolveAsync(string storeId);
}
