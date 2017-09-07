using System;
using System.Threading.Tasks;
using VirtoCommerce.Domain.Order.Events;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Ovservers
{
    public class OrderChangedObserver : IObserver<OrderChangedEvent>
    {
        private readonly IGoogleAnalyticsTransactionManager _gaTransactionManager;

        public OrderChangedObserver(IGoogleAnalyticsTransactionManager gaTransactionManager)
        {
            _gaTransactionManager = gaTransactionManager;
        }

        public void OnNext(OrderChangedEvent value)
        {
			if (value.ChangeState == Platform.Core.Common.EntryState.Added)
			{
				System.Threading.Tasks.Task.Factory.StartNew(s => ((IGoogleAnalyticsTransactionManager)s).CreateTransaction(value.ModifiedOrder), _gaTransactionManager, System.Threading.CancellationToken.None, System.Threading.Tasks.TaskCreationOptions.None, System.Threading.Tasks.TaskScheduler.Default).Unwrap().GetAwaiter().GetResult();
			}
			else if (value.ChangeState == Platform.Core.Common.EntryState.Modified)
            {
                if (value.ModifiedOrder.Status == "Cancelled")
                {
					System.Threading.Tasks.Task.Factory.StartNew(s => ((IGoogleAnalyticsTransactionManager)s).RevertTransaction(value.ModifiedOrder), _gaTransactionManager, System.Threading.CancellationToken.None, System.Threading.Tasks.TaskCreationOptions.None, System.Threading.Tasks.TaskScheduler.Default).Unwrap().GetAwaiter().GetResult();
				}
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}