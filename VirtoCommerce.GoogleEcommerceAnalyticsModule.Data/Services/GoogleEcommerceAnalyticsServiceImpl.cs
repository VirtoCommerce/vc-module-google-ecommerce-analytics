using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Converters;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Repositories;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services
{
    public class GoogleEcommerceAnalyticsServiceImpl : ServiceBase, IGoogleEcommerceAnalyticsService
    {
        private readonly Func<IGoogleEcommerceAnalyticsRepository> _googleEcommerceAnalyticsRepositoryFactory;

        public GoogleEcommerceAnalyticsServiceImpl(Func<IGoogleEcommerceAnalyticsRepository> googleEcommerceAnalyticsRepositoryFactory)
        {
            if (googleEcommerceAnalyticsRepositoryFactory == null)
                throw new ArgumentNullException("googleEcommerceAnalyticsRepositoryFactory");

            _googleEcommerceAnalyticsRepositoryFactory = googleEcommerceAnalyticsRepositoryFactory;
        }

        public StoreSettings GetSettingsByStoreId(string storeId)
        {
            return _googleEcommerceAnalyticsRepositoryFactory().GetSettingsByStoreId(storeId);
        }

        public void AddOrUpdate(StoreSettings settings)
        {
            using (var repository = _googleEcommerceAnalyticsRepositoryFactory())
            using (var changeTracker = GetChangeTracker(repository))
            {
                if (!settings.IsTransient())
                {
                    var existList = repository.GetSettingsByStoreId(settings.StoreId);
                    if (existList != null)
                    {
                        changeTracker.Attach(existList);
                        settings.Patch(existList);
                    }
                    else
                    {
                        repository.Add(settings);
                    }
                }
                else
                {
                    repository.Add(settings);
                }
                repository.UnitOfWork.Commit();
            }
        }
    }
}
