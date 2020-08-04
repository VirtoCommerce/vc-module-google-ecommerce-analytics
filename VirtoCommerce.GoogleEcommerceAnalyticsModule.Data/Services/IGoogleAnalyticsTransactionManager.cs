using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
    public interface IGoogleAnalyticsTransactionManager
    {
        Task RevertTransactionAsync(CustomerOrder order);
        Task CreateTransactionAsync(CustomerOrder origOrder);
    }
}