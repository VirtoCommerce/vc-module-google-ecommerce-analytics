using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.Platform.Data.Infrastructure.Interceptors;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Repositories
{
    public class GoogleEcommerceAnaliticsRepositoryImpl : EFRepositoryBase, IGoogleEcommerceAnalyticsRepository
    {
        public GoogleEcommerceAnaliticsRepositoryImpl()
        {
        }

        public GoogleEcommerceAnaliticsRepositoryImpl(string nameOrConnectionString, params IInterceptor[] interceptors)
			: base(nameOrConnectionString, null, interceptors)
		{
            Configuration.LazyLoadingEnabled = false;
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Entity<StoreSettings>().HasKey(x => x.Id)
                        .Property(x => x.Id);

            modelBuilder.Entity<StoreSettings>().ToTable("GoogleEcommerceAnalitics");
        }

        public IQueryable<StoreSettings> StoreSettings
        {
            get { return GetAsQueryable<StoreSettings>(); }
        }

        public StoreSettings GetSettingsByStoreId(string storeId)
        {
            return StoreSettings.FirstOrDefault(s => s.StoreId == storeId);
        }
    }
}
