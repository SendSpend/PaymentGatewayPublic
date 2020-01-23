using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.SendSpendManual.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Fields.AppUrl")]
        public string AppUrl { get; set; }
        public bool AppUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Fields.NotifyUrl")]
        public string NotifyUrl { get; set; }
        public bool NotifyUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Fields.ContinueUrl")]
        public string ContinueUrl { get; set; }
        public bool ContinueUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Fields.MerchantAppId")]
        public string AppId { get; set; }
        public bool AppId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Fields.MerchantName")]
        public string MerchantName { get; set; }
        public bool MerchantName_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Fields.MerchantUniqueId")]
        public string MerchantUniqueId { get; set; }
        public bool MerchantUniqueId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Fields.MerchantBaseUrl")]
        public string MerchantBaseUrl { get; set; }
        public bool MerchantBaseUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Fields.MerchantOrderAlive")]
        public int MerchantOrderAlive { get; set; }
        public bool MerchantOrderAlive_OverrideForStore { get; set; }
    }
}