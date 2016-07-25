using webModels = VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Models;
using coreModels = VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using Omu.ValueInjecter;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Web.Converters
{
    public static class StoreSettingsConverter
    {
        public static coreModels.StoreSettings ToCoreModel(this webModels.StoreSettings settings)
        {
            var retVal = new coreModels.StoreSettings();
            retVal.InjectFrom(settings);
            return retVal;
        }

        public static webModels.StoreSettings ToWebModel(this coreModels.StoreSettings settings)
        {
            var retVal = new webModels.StoreSettings();
            retVal.InjectFrom(settings);
            return retVal;
        }
    }
}