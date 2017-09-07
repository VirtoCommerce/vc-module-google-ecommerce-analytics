using System.Threading.Tasks;
using VirtoCommerce.Domain.Order.Model;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
    public interface IGoogleAnalyticsTransactionManager
    {
		Task RevertTransaction(CustomerOrder order);
		Task CreateTransaction(CustomerOrder origOrder);
	}
}