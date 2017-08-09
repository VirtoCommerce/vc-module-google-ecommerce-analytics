using VirtoCommerce.Domain.Order.Model;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
    public interface IGoogleAnalyticsTransactionManager
    {
        void RevertTransaction(CustomerOrder order);
    }
}