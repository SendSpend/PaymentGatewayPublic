using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.SendSpendManual.Models
{
    public class ApiAuthRequest
    {
        public string apiId { get; set; }
        public string customer_fmm_UserNameOrMobileNo { get; set; }
    }

    public class PaymentInfoModel : BaseNopModel
    {
        public PaymentInfoModel()
        {
            Countries = new List<SelectListItem>();
        }

        [AllowHtml]
        public string Country { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.Country")]
        public IList<SelectListItem> Countries { get; set; }

        [NopResourceDisplayName("Plugins.Payments.FMMManual.PhoneNumber")]
        [AllowHtml]
        public string PhoneNumber { get; set; }
    }

    public class ApiPaymentRequest
    {
        public ApiPaymentRequest()
        {
            merch_Products = new List<ApiProduct>();
        }

        public string apiId { get; set; }
        public string merch_name { get; set; }
        public string merch_uniqueId { get; set; }
        public string merch_baseURL { get; set; }
        public string merch_orderId { get; set; }
        public string merch_orderDescription { get; set; }
        public decimal merch_subTotal { get; set; }
        public decimal merch_totalAmount { get; set; }
        public string merch_currency { get; set; }
        public System.DateTime merch_orderDateTime { get; set; }
        public string merch_notifyUrl { get; set; }
        public string merch_continueUrl { get; set; }
        public string customer_firstName { get; set; }
        public string customer_lastName { get; set; }
        public string customer_Email { get; set; }
        public string customer_fmm_UserNameOrMobileNo { get; set; }
        public List<ApiProduct> merch_Products { get; set; }
        public int merch_order_alive { get; set; }
        public string merch_shortOrderNo { get; set; }
        public decimal merch_shippingCost { get; set; }
        public decimal merch_otherCharges { get; set; }
    }

    public class ApiProduct
    {
        public string merch_productId { get; set; }
        public string merch_productName { get; set; }
        public int merch_productQty { get; set; }
        public decimal merch_productUnitPrice { get; set; }
    }

    public class ApiResponse {
        public string userId { get; set; }
        public bool isSuccess { get; set; }
        public string responseCode { get; set; }
        public string responseText { get; set; }
        public object error { get; set; }

    }
}