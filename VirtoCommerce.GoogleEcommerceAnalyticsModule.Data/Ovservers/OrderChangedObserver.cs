using System;
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
            if (value.ChangeState == Platform.Core.Common.EntryState.Modified)
            {
                if (value.ModifiedOrder.Status == "Cancelled")
                {
                    _gaTransactionManager.RevertTransaction(value.ModifiedOrder);
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