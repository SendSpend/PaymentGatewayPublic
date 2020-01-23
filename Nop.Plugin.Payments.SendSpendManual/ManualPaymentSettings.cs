using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.SendSpend
{
    public class SendSpendPaymentSettings : ISettings
    {
        public string AppUrl { get; set; }
        public string AppId { get; set; }
        public string MerchantName { get; set; }
        public string MerchantUniqueId { get; set; }
        public string MerchantBaseUrl { get; set; }
        public string NotifyUrl { get; set; }
        public string ContinueUrl { get; set; }
        public int MerchantOrderAlive { get; set; }
        public bool PreAuth { get; set; }
    }
}
