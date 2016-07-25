using System;
using System.Configuration;
using System.Linq;
using VirtoCommerce.Platform.Core.Modularity;
using Microsoft.Practices.Unity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Repositories;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Security;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.Platform.Data.Infrastructure.Interceptors;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web
{
    public class Module : ModuleBase
    {
        private const string _connectionStringName = "VirtoCommerce";
        private readonly IUnityContainer _container;

        public Module(IUnityContainer container)
        {
            _container = container;
        }

        #region Public Methods and Operators

        public override void Initialize()
        {
            Func<IGoogleEcommerceAnalyticsRepository> menuRepFactory = () =>
                new GoogleEcommerceAnaliticsRepositoryImpl(_connectionStringName, _container.Resolve<AuditableInterceptor>(), new EntityPrimaryKeyGeneratorInterceptor());

            _container.RegisterInstance(menuRepFactory);
            _container.RegisterType<IGoogleEcommerceAnalyticsService, GoogleEcommerceAnalyticsServiceImpl>();
        }

        public override void PostInitialize()
        {
            base.PostInitialize();

            //Register bounded security scope types
            var securityScopeService = _container.Resolve<IPermissionScopeService>();
            securityScopeService.RegisterSope(() => new GoogleEcommerceAnalyticsStoreScope());
        }

        public override void SetupDatabase()
        {
            base.SetupDatabase();

            using (var context = new GoogleEcommerceAnaliticsRepositoryImpl(_connectionStringName, _container.Resolve<AuditableInterceptor>()))
            {
                var initializer = new SetupDatabaseInitializer<GoogleEcommerceAnaliticsRepositoryImpl, Data.Migrations.Configuration>();
                initializer.InitializeDatabase(context);
            }
        }

        #endregion
    }
}
