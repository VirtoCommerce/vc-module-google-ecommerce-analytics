using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Handlers
{
    public class OrderChangedHandler : IEventHandler<OrderChangedEvent>
    {
        private readonly IGoogleAnalyticsTransactionManager _gaTransactionManager;

        public OrderChangedHandler(IGoogleAnalyticsTransactionManager gaTransactionManager)
        {
            _gaTransactionManager = gaTransactionManager;
        }

        public async Task Handle(OrderChangedEvent @event)
        {
            foreach (var changedEntry in @event.ChangedEntries)
            {
                if (changedEntry.EntryState == Platform.Core.Common.EntryState.Added)
                {
                    Task.Factory.StartNew(s => ((IGoogleAnalyticsTransactionManager)s).CreateTransactionAsync(changedEntry.NewEntry), _gaTransactionManager, System.Threading.CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Unwrap().GetAwaiter().GetResult();
                }
                else if (changedEntry.EntryState == Platform.Core.Common.EntryState.Modified && changedEntry.NewEntry.Status == "Cancelled")
                {
                    Task.Factory.StartNew(s => ((IGoogleAnalyticsTransactionManager)s).RevertTransactionAsync(changedEntry.NewEntry), _gaTransactionManager, System.Threading.CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Unwrap().GetAwaiter().GetResult();
                }
            }
        }
    }
}