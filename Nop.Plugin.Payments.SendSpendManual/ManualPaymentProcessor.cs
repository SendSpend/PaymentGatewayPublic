using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Routing;
using System.Linq;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Plugins;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
using Nop.Plugin.Payments.SendSpend.Controllers;


namespace Nop.Plugin.Payments.SendSpend
{
    public class SendSpendPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly ICustomerService _customerService;
        private readonly ICurrencyService _currencyService;
        private readonly IPaymentService _paymentService;
        private readonly IWebHelper _webHelper;
        private readonly SendSpendPaymentSettings _manualPaymentSettings;
        private readonly CurrencySettings _currencySettings;
        private readonly IWorkContext _workContext;
        private readonly ITaxService _taxService;


        #endregion

        #region Ctor

        public SendSpendPaymentProcessor(ILocalizationService localizationService,
            IOrderTotalCalculationService orderTotalCalculationService,
            ISettingService settingService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IPaymentService paymentService,
            SendSpendPaymentSettings manualPaymentSettings,
            CurrencySettings currencySettings,
            IWebHelper webHelper,
            IWorkContext workContext,
            ITaxService taxService
            )
        {
            this._localizationService = localizationService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._customerService = customerService;
            this._paymentService = paymentService;
            this._webHelper = webHelper;
            this._manualPaymentSettings = manualPaymentSettings;
            this._currencySettings = currencySettings;
            this._workContext = workContext;
            this._taxService = taxService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            decimal subTotal = 0;
            decimal? shipping = 0;
            decimal taxTotal = 0;
            SortedDictionary<decimal, decimal> taxRatesDictionary;
            var _products = new List<Models.ApiProduct>();
            var NotifyUrl = _manualPaymentSettings.MerchantBaseUrl + "PaymentSendSpend/IPNHandler/?order={0}&status={1}";
            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);
            if (customer == null)
                throw new Exception("Customer cannot be loaded");

            var shoppingCart = customer.ShoppingCartItems
                    .Where(shoppingCartItem => shoppingCartItem.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .LimitPerStore(processPaymentRequest.StoreId).ToList();
            shipping = _orderTotalCalculationService.GetShoppingCartShippingTotal(shoppingCart, false);
            taxTotal = _orderTotalCalculationService.GetTaxTotal(shoppingCart, out taxRatesDictionary);
            subTotal = shoppingCart.Sum(x => x.Product.Price * x.Quantity);
            shoppingCart.ForEach(x =>
            {
                _products.Add(new Models.ApiProduct
                {
                    merch_productId = x.ProductId.ToString(),
                    merch_productName = x.Product.Name,
                    merch_productQty = x.Quantity,
                    merch_productUnitPrice = _currencyService.ConvertFromPrimaryStoreCurrency(x.Product.Price, _workContext.WorkingCurrency)
                });
            });

            var apiPayment = new Models.ApiPaymentRequest
            {
                merch_name = _manualPaymentSettings.MerchantName,
                apiId = _manualPaymentSettings.AppId,
                merch_uniqueId = _manualPaymentSettings.MerchantUniqueId,
                merch_baseURL = _manualPaymentSettings.MerchantBaseUrl,
                merch_orderId = processPaymentRequest.OrderGuid.ToString(),
                merch_orderDescription = processPaymentRequest.InitialOrderId > 0 ? string.Format("Parent Order Id: {0}", processPaymentRequest.InitialOrderId) : "",
                merch_subTotal = _currencyService.ConvertFromPrimaryStoreCurrency(subTotal, _workContext.WorkingCurrency),
                merch_totalAmount = _currencyService.ConvertFromPrimaryStoreCurrency(processPaymentRequest.OrderTotal, _workContext.WorkingCurrency),
                merch_currency = processPaymentRequest.CustomValues["Currency"].ToString(),
                merch_orderDateTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")),
                merch_notifyUrl = NotifyUrl,
                merch_continueUrl = _manualPaymentSettings.ContinueUrl,
                merch_Products = _products,
                customer_firstName = customer.BillingAddress.FirstName,
                customer_lastName = customer.BillingAddress.LastName,
                customer_Email = customer.BillingAddress.Email,
                customer_fmm_UserNameOrMobileNo = processPaymentRequest.CustomValues["PhoneNumber"].ToString(),
                merch_order_alive = _manualPaymentSettings.MerchantOrderAlive,
                merch_shortOrderNo = string.Empty,
                merch_shippingCost = shipping.HasValue ? _currencyService.ConvertFromPrimaryStoreCurrency(shipping.Value, _workContext.WorkingCurrency) : 0,
                merch_otherCharges = _currencyService.ConvertFromPrimaryStoreCurrency(taxTotal, _workContext.WorkingCurrency),
                merch_preAuth = _manualPaymentSettings.PreAuth,
                countryCode = processPaymentRequest.CustomValues["Country"].ToString()
            };

            using (var client = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(apiPayment));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using (var res = client.PostAsync(String.Format("{0}api/v1/SecurePayment", _manualPaymentSettings.AppUrl), content))
                {
                    res.Wait();
                    if (res.Result.IsSuccessStatusCode)
                    {
                        var data = res.Result.Content.ReadAsStringAsync();
                        var ret = JsonConvert.DeserializeObject<Models.ApiResponse>(data.Result.ToString());
                        switch (ret.responseCode.ToUpper())
                        {
                            case "APPROVED":
                                result.NewPaymentStatus = Core.Domain.Payments.PaymentStatus.Pending;
                                break;
                            default:
                                result.AddError(ret.responseText);
                                break;
                        }
                    }
                    else
                    {
                        result.AddError("Unable to process your request, Please try again later.");
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //Not required
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart, 0, false);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            var apiPayment = new Models.PaymentOrderMechantUpdate
            {
                apiId = _manualPaymentSettings.AppId,
                merch_Id = _manualPaymentSettings.MerchantUniqueId,
                merch_orderId = capturePaymentRequest.Order.OrderGuid.ToString(),
                order_status = "CAPTURE"
            };

            using (var client = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(apiPayment));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using (var res = client.PostAsync(String.Format("{0}api/v1/SecurePayment/UpdateOrder", _manualPaymentSettings.AppUrl), content))
                {
                    res.Wait();
                    if (res.Result.IsSuccessStatusCode)
                    {
                        var data = res.Result.Content.ReadAsStringAsync();
                        var ret = JsonConvert.DeserializeObject<Models.ApiResponse>(data.Result.ToString());
                        switch (ret.responseCode.ToUpper())
                        {
                            case "APPROVED":
                                result.NewPaymentStatus = Core.Domain.Payments.PaymentStatus.Paid;
                                break;
                            default:
                                result.AddError(ret.responseText);
                                break;
                        }
                    }
                    else
                    {
                        result.AddError("Unable to process your request, Please try again later.");
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            var apiPayment = new Models.PaymentOrderMechantUpdate
            {
                apiId = _manualPaymentSettings.AppId,
                merch_Id = _manualPaymentSettings.MerchantUniqueId,
                merch_orderId = refundPaymentRequest.Order.OrderGuid.ToString(),
                order_status = "CANCELLED"
            };

            using (var client = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(apiPayment));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using (var res = client.PostAsync(String.Format("{0}api/v1/SecurePayment/UpdateOrder", _manualPaymentSettings.AppUrl), content))
                {
                    res.Wait();
                    if (res.Result.IsSuccessStatusCode)
                    {
                        var data = res.Result.Content.ReadAsStringAsync();
                        var ret = JsonConvert.DeserializeObject<Models.ApiResponse>(data.Result.ToString());
                        switch (ret.responseCode.ToUpper())
                        {
                            case "APPROVED":
                                result.NewPaymentStatus = Core.Domain.Payments.PaymentStatus.Voided;
                                break;
                            default:
                                result.AddError(ret.responseText);
                                break;
                        }
                    }
                    else
                    {
                        result.AddError("Unable to process your request, Please try again later.");
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            var apiPayment = new Models.PaymentOrderMechantUpdate
            {
                apiId = _manualPaymentSettings.AppId,
                merch_Id = _manualPaymentSettings.MerchantUniqueId,
                merch_orderId = voidPaymentRequest.Order.OrderGuid.ToString(),
                order_status = "CANCELLED"
            };

            using (var client = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(apiPayment));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using (var res = client.PostAsync(String.Format("{0}api/v1/SecurePayment/UpdateOrder", _manualPaymentSettings.AppUrl), content))
                {
                    res.Wait();
                    if (res.Result.IsSuccessStatusCode)
                    {
                        var data = res.Result.Content.ReadAsStringAsync();
                        var ret = JsonConvert.DeserializeObject<Models.ApiResponse>(data.Result.ToString());
                        switch (ret.responseCode.ToUpper())
                        {
                            case "APPROVED":
                                result.NewPaymentStatus = Core.Domain.Payments.PaymentStatus.Voided;
                                break;
                            default:
                                result.AddError(ret.responseText);
                                break;
                        }
                    }
                    else
                    {
                        result.AddError("Unable to process your request, Please try again later.");
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            //always success
            return new CancelRecurringPaymentResult();
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //it's not a redirection payment method. So we always return false
            return false;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentSendSpend";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.SendSpend.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentSendSpend";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.SendSpend.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Get the type of controller
        /// </summary>
        /// <returns>Type</returns>
        public Type GetControllerType()
        {
            return typeof(PaymentSendSpendController);
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new SendSpendPaymentSettings
            {
                MerchantOrderAlive = 5,
                MerchantBaseUrl = _webHelper.GetStoreLocation(),
                NotifyUrl = _webHelper.GetStoreLocation() + "PaymentSendSpend/IPNHandler/?order={0}&status={1}",
                AppId = "2ee452f4-2ce8-483e-bd67-8d999cd5b33e",
                PreAuth = false
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.AppUrl", "Base App Url");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantAppId", "Merchant App Id");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantName", "Merchant Name");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantUniqueId", "Merchant UniqueId");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantBaseUrl", "Merchant Base Url");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.NotifyUrl", "Notify Url");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.ContinueUrl", "Continue Url");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantOrderAlive", "Order alive");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.PreAuth", "Required Pre Authorization");


            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantUniqueId.Hint", "Merchant wallet Id provided by SendSpend.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantOrderAlive.Hint", "Time to expire pending order; if user did not complete the transaction on time.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Fields.PreAuth.Hint", "Pre authorization required after customer payment.");


            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Country", "Please select your country:");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.PhoneNumber", "Enter your registered phone number:");
            //this.AddOrUpdatePluginLocaleResource("plugins.payments.sendspendmanual.paymentmethoddescription", "Pay by Send Spend.");
            this.AddOrUpdatePluginLocaleResource("plugins.payments.sendspend.paymentmethoddescription", "After checkout please authorize transaction with your phone."); //Abdul change this like 22/12/2017 at 10am.
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.Country.Required", "Country is required.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.PhoneNumber.Required", "Phone number is not valid");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.SendSpend.PhoneNumber.Wrong", "Phone number is not valid");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<SendSpendPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.AppUrl");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantAppId");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantName");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantUniqueId");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantBaseUrl");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.NotifyUrl");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.ContinueUrl");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantOrderAlive");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.PreAuth");

            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantUniqueId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.MerchantOrderAlive.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Fields.PreAuth.Hint");

            this.DeletePluginLocaleResource("plugins.payments.sendspend.paymentmethoddescription");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.Country.Required");
            this.DeletePluginLocaleResource("Plugins.Payments.SendSpend.PhoneNumber.Wrong");


            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.Manual; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Standard; }
        }
        
        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.SendSpend.PaymentMethodDescription"); }
        }

        #endregion

    }
}
