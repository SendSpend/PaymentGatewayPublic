using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Mvc;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Plugin.Payments.SendSpendManual.Models;
using Nop.Plugin.Payments.SendSpendManual.Validators;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.SendSpendManual.Controllers
{
    public class PaymentSendSpendManualController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly Core.Domain.Payments.PaymentSettings _paymentSettings;
        private readonly Services.Logging.ILogger _logger;

        public PaymentSendSpendManualController(IWorkContext workContext,
            IStoreService storeService, 
            ISettingService settingService, 
            ILocalizationService localizationService,
            Core.Domain.Payments.PaymentSettings paymentSettings,
            IOrderService orderService,
            IPaymentService paymentService,
            Services.Logging.ILogger logger
            )
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._localizationService = localizationService;
            this._paymentSettings = paymentSettings;
            this._orderService = orderService;
            this._paymentService = paymentService;
            this._logger = logger;
        }
        
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var manualPaymentSettings = _settingService.LoadSetting<SendSpendManualPaymentSettings>(storeScope);

            var model = new ConfigurationModel();

            model.AppUrl = manualPaymentSettings.AppUrl;
            //model.AppId = manualPaymentSettings.AppId;
            model.MerchantName = manualPaymentSettings.MerchantName;
            model.MerchantUniqueId = manualPaymentSettings.MerchantUniqueId;
            model.MerchantBaseUrl = manualPaymentSettings.MerchantBaseUrl;
            //model.NotifyUrl = manualPaymentSettings.NotifyUrl;
            model.ContinueUrl = manualPaymentSettings.ContinueUrl;
            model.MerchantOrderAlive = manualPaymentSettings.MerchantOrderAlive;


            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.AppId_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.AppId, storeScope);
                model.MerchantName_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.MerchantName, storeScope);
                model.MerchantUniqueId_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.MerchantUniqueId, storeScope);
                model.MerchantBaseUrl_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.MerchantBaseUrl, storeScope);
                model.AppUrl_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.AppUrl, storeScope);
                model.NotifyUrl_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.NotifyUrl, storeScope);
                model.ContinueUrl_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.ContinueUrl, storeScope);
                model.MerchantOrderAlive_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.MerchantOrderAlive, storeScope);
            }

            return View("~/Plugins/Payments.SendSpendManual/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var manualPaymentSettings = _settingService.LoadSetting<SendSpendManualPaymentSettings>(storeScope);

            //save settings
            manualPaymentSettings.AppUrl = model.AppUrl;
            //manualPaymentSettings.AppId = model.AppId;
            manualPaymentSettings.MerchantName = model.MerchantName;
            manualPaymentSettings.MerchantUniqueId = model.MerchantUniqueId;
            manualPaymentSettings.MerchantBaseUrl = model.MerchantBaseUrl;
            //manualPaymentSettings.NotifyUrl = model.NotifyUrl;
            manualPaymentSettings.ContinueUrl = model.ContinueUrl;
            manualPaymentSettings.MerchantOrderAlive = model.MerchantOrderAlive;
            
            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */


            _settingService.SaveSettingOverridablePerStore(manualPaymentSettings, x => x.AppUrl, model.AppUrl_OverrideForStore, storeScope, false);
            //_settingService.SaveSettingOverridablePerStore(manualPaymentSettings, x => x.AppId, model.AppId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(manualPaymentSettings, x => x.MerchantName, model.MerchantName_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(manualPaymentSettings, x => x.MerchantUniqueId, model.MerchantUniqueId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(manualPaymentSettings, x => x.MerchantBaseUrl, model.MerchantBaseUrl_OverrideForStore, storeScope, false);
            //_settingService.SaveSettingOverridablePerStore(manualPaymentSettings, x => x.NotifyUrl, model.NotifyUrl_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(manualPaymentSettings, x => x.ContinueUrl, model.ContinueUrl_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(manualPaymentSettings, x => x.MerchantOrderAlive, model.MerchantOrderAlive_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();

            model.Countries.Add(new SelectListItem { Text = "", Value = "" });
            model.Countries.Add(new SelectListItem { Text = "Angola (+244)", Value = "244" });
            model.Countries.Add(new SelectListItem { Text = "Andorra (+376)", Value = "376" });
            model.Countries.Add(new SelectListItem { Text = "Algeria (+213)", Value = "213" });
            model.Countries.Add(new SelectListItem { Text = "Anguilla (+1264)", Value = "1264" });
            model.Countries.Add(new SelectListItem { Text = "Antigua & Barbuda (+1268)", Value = "1268" });
            model.Countries.Add(new SelectListItem { Text = "Argentina (+54)", Value = "54" });
            model.Countries.Add(new SelectListItem { Text = "Armenia (+374)", Value = "374" });
            model.Countries.Add(new SelectListItem { Text = "Aruba (+297)", Value = "297" });
            model.Countries.Add(new SelectListItem { Text = "Australia (+61)", Value = "61" });
            model.Countries.Add(new SelectListItem { Text = "Austria (+43)", Value = "43" });
            model.Countries.Add(new SelectListItem { Text = "Azerbaijan (+994)", Value = "994" });
            model.Countries.Add(new SelectListItem { Text = "Bahamas (+1242)", Value = "1242" });
            model.Countries.Add(new SelectListItem { Text = "Bahrain (+973)", Value = "973" });
            model.Countries.Add(new SelectListItem { Text = "Bangladesh (+880)", Value = "880" });
            model.Countries.Add(new SelectListItem { Text = "Barbados (+1246)", Value = "1246" });
            model.Countries.Add(new SelectListItem { Text = "Belarus (+375)", Value = "375" });
            model.Countries.Add(new SelectListItem { Text = "Belgium (+32)", Value = "32" });
            model.Countries.Add(new SelectListItem { Text = "Belize (+501)", Value = "501" });
            model.Countries.Add(new SelectListItem { Text = "Benin (+229)", Value = "229" });
            model.Countries.Add(new SelectListItem { Text = "Bermuda (+1441)", Value = "1441" });
            model.Countries.Add(new SelectListItem { Text = "Bhutan (+975)", Value = "975" });
            model.Countries.Add(new SelectListItem { Text = "Bolivia (+591)", Value = "591" });
            model.Countries.Add(new SelectListItem { Text = "Bosnia Herzegovina (+387)", Value = "387" });
            model.Countries.Add(new SelectListItem { Text = "Botswana (+267)", Value = "267" });
            model.Countries.Add(new SelectListItem { Text = "Brazil (+55)", Value = "55" });
            model.Countries.Add(new SelectListItem { Text = "Brunei (+673)", Value = "673" });
            model.Countries.Add(new SelectListItem { Text = "Bulgaria (+359)", Value = "359" });
            model.Countries.Add(new SelectListItem { Text = "Burkina Faso (+226)", Value = "226" });
            model.Countries.Add(new SelectListItem { Text = "Burundi (+257)", Value = "257" });
            model.Countries.Add(new SelectListItem { Text = "Cambodia (+855)", Value = "855" });
            model.Countries.Add(new SelectListItem { Text = "Cameroon (+237)", Value = "237" });
            model.Countries.Add(new SelectListItem { Text = "Canada (+1)", Value = "1" });
            model.Countries.Add(new SelectListItem { Text = "Cape Verde Islands (+238)", Value = "238" });
            model.Countries.Add(new SelectListItem { Text = "Cayman Islands (+1345)", Value = "1345" });
            model.Countries.Add(new SelectListItem { Text = "Central African Republic (+236)", Value = "236" });
            model.Countries.Add(new SelectListItem { Text = "Chile (+56)", Value = "56" });
            model.Countries.Add(new SelectListItem { Text = "China (+86)", Value = "86" });
            model.Countries.Add(new SelectListItem { Text = "Colombia (+57)", Value = "57" });
            model.Countries.Add(new SelectListItem { Text = "Comoros (+269)", Value = "269" });
            model.Countries.Add(new SelectListItem { Text = "Congo (+242)", Value = "242" });
            model.Countries.Add(new SelectListItem { Text = "Cook Islands (+682)", Value = "682" });
            model.Countries.Add(new SelectListItem { Text = "Costa Rica (+506)", Value = "506" });
            model.Countries.Add(new SelectListItem { Text = "Croatia (+385)", Value = "385" });
            model.Countries.Add(new SelectListItem { Text = "Cuba (+53)", Value = "53" });
            model.Countries.Add(new SelectListItem { Text = "Cyprus North (+90392)", Value = "90392" });
            model.Countries.Add(new SelectListItem { Text = "Cyprus South (+357)", Value = "357" });
            model.Countries.Add(new SelectListItem { Text = "Czech Republic (+42)", Value = "42" });
            model.Countries.Add(new SelectListItem { Text = "Denmark (+45)", Value = "45" });
            model.Countries.Add(new SelectListItem { Text = "Djibouti (+253)", Value = "253" });
            model.Countries.Add(new SelectListItem { Text = "Dominica (+1809)", Value = "1809" });
            model.Countries.Add(new SelectListItem { Text = "Dominican Republic (+1809)", Value = "1809" });
            model.Countries.Add(new SelectListItem { Text = "Ecuador (+593)", Value = "593" });
            model.Countries.Add(new SelectListItem { Text = "Egypt (+20)", Value = "20" });
            model.Countries.Add(new SelectListItem { Text = "El Salvador (+503)", Value = "503" });
            model.Countries.Add(new SelectListItem { Text = "Equatorial Guinea (+240)", Value = "240" });
            model.Countries.Add(new SelectListItem { Text = "Eritrea (+291)", Value = "291" });
            model.Countries.Add(new SelectListItem { Text = "Estonia (+372)", Value = "372" });
            model.Countries.Add(new SelectListItem { Text = "Ethiopia (+251)", Value = "251" });
            model.Countries.Add(new SelectListItem { Text = "Falkland Islands (+500)", Value = "500" });
            model.Countries.Add(new SelectListItem { Text = "Faroe Islands (+298)", Value = "298" });
            model.Countries.Add(new SelectListItem { Text = "Fiji (+679)", Value = "679" });
            model.Countries.Add(new SelectListItem { Text = "Finland (+358)", Value = "358" });
            model.Countries.Add(new SelectListItem { Text = "France (+33)", Value = "33" });
            model.Countries.Add(new SelectListItem { Text = "French Guiana (+594)", Value = "594" });
            model.Countries.Add(new SelectListItem { Text = "French Polynesia (+689)", Value = "689" });
            model.Countries.Add(new SelectListItem { Text = "Gabon (+241)", Value = "241" });
            model.Countries.Add(new SelectListItem { Text = "Gambia (+220)", Value = "220" });
            model.Countries.Add(new SelectListItem { Text = "Georgia (+7880)", Value = "7880" });
            model.Countries.Add(new SelectListItem { Text = "Germany (+49)", Value = "49" });
            model.Countries.Add(new SelectListItem { Text = "Ghana (+233)", Value = "233" });
            model.Countries.Add(new SelectListItem { Text = "Gibraltar (+350)", Value = "350" });
            model.Countries.Add(new SelectListItem { Text = "Greece (+30)", Value = "30" });
            model.Countries.Add(new SelectListItem { Text = "Greenland (+299)", Value = "299" });
            model.Countries.Add(new SelectListItem { Text = "Grenada (+1473)", Value = "1473" });
            model.Countries.Add(new SelectListItem { Text = "Guadeloupe (+590)", Value = "590" });
            model.Countries.Add(new SelectListItem { Text = "Guam (+671)", Value = "671" });
            model.Countries.Add(new SelectListItem { Text = "Guatemala (+502)", Value = "502" });
            model.Countries.Add(new SelectListItem { Text = "Guinea (+224)", Value = "224" });
            model.Countries.Add(new SelectListItem { Text = "Guinea - Bissau (+245)", Value = "245" });
            model.Countries.Add(new SelectListItem { Text = "Guyana (+592)", Value = "592" });
            model.Countries.Add(new SelectListItem { Text = "Haiti (+509)", Value = "509" });
            model.Countries.Add(new SelectListItem { Text = "Honduras (+504)", Value = "504" });
            model.Countries.Add(new SelectListItem { Text = "Hong Kong (+852)", Value = "852" });
            model.Countries.Add(new SelectListItem { Text = "Hungary (+36)", Value = "36" });
            model.Countries.Add(new SelectListItem { Text = "Iceland (+354)", Value = "354" });
            model.Countries.Add(new SelectListItem { Text = "India (+91)", Value = "91" });
            model.Countries.Add(new SelectListItem { Text = "Indonesia (+62)", Value = "62" });
            model.Countries.Add(new SelectListItem { Text = "Iran (+98)", Value = "98" });
            model.Countries.Add(new SelectListItem { Text = "Iraq (+964)", Value = "964" });
            model.Countries.Add(new SelectListItem { Text = "Ireland (+353)", Value = "353" });
            model.Countries.Add(new SelectListItem { Text = "Israel (+972)", Value = "972" });
            model.Countries.Add(new SelectListItem { Text = "Italy (+39)", Value = "39" });
            model.Countries.Add(new SelectListItem { Text = "Jamaica (+1876)", Value = "1876" });
            model.Countries.Add(new SelectListItem { Text = "Japan (+81)", Value = "81" });
            model.Countries.Add(new SelectListItem { Text = "Jordan (+962)", Value = "962" });
            model.Countries.Add(new SelectListItem { Text = "Kazakhstan (+7)", Value = "7" });
            model.Countries.Add(new SelectListItem { Text = "Kenya (+254)", Value = "254" });
            model.Countries.Add(new SelectListItem { Text = "Kiribati (+686)", Value = "686" });
            model.Countries.Add(new SelectListItem { Text = "Korea North (+850)", Value = "850" });
            model.Countries.Add(new SelectListItem { Text = "Korea South (+82)", Value = "82" });
            model.Countries.Add(new SelectListItem { Text = "Kuwait (+965)", Value = "965" });
            model.Countries.Add(new SelectListItem { Text = "Kyrgyzstan (+996)", Value = "996" });
            model.Countries.Add(new SelectListItem { Text = "Laos (+856)", Value = "856" });
            model.Countries.Add(new SelectListItem { Text = "Latvia (+371)", Value = "371" });
            model.Countries.Add(new SelectListItem { Text = "Lebanon (+961)", Value = "961" });
            model.Countries.Add(new SelectListItem { Text = "Lesotho (+266)", Value = "266" });
            model.Countries.Add(new SelectListItem { Text = "Liberia (+231)", Value = "231" });
            model.Countries.Add(new SelectListItem { Text = "Libya (+218)", Value = "218" });
            model.Countries.Add(new SelectListItem { Text = "Liechtenstein (+417)", Value = "417" });
            model.Countries.Add(new SelectListItem { Text = "Lithuania (+370)", Value = "370" });
            model.Countries.Add(new SelectListItem { Text = "Luxembourg (+352)", Value = "352" });
            model.Countries.Add(new SelectListItem { Text = "Macao (+853)", Value = "853" });
            model.Countries.Add(new SelectListItem { Text = "Macedonia (+389)", Value = "389" });
            model.Countries.Add(new SelectListItem { Text = "Madagascar (+261)", Value = "261" });
            model.Countries.Add(new SelectListItem { Text = "Malawi (+265)", Value = "265" });
            model.Countries.Add(new SelectListItem { Text = "Malaysia (+60)", Value = "60" });
            model.Countries.Add(new SelectListItem { Text = "Maldives (+960)", Value = "960" });
            model.Countries.Add(new SelectListItem { Text = "Mali (+223)", Value = "223" });
            model.Countries.Add(new SelectListItem { Text = "Malta (+356)", Value = "356" });
            model.Countries.Add(new SelectListItem { Text = "Marshall Islands (+692)", Value = "692" });
            model.Countries.Add(new SelectListItem { Text = "Martinique (+596)", Value = "596" });
            model.Countries.Add(new SelectListItem { Text = "Mauritania (+222)", Value = "222" });
            model.Countries.Add(new SelectListItem { Text = "Mayotte (+269)", Value = "269" });
            model.Countries.Add(new SelectListItem { Text = "Mexico (+52)", Value = "52" });
            model.Countries.Add(new SelectListItem { Text = "Micronesia (+691)", Value = "691" });
            model.Countries.Add(new SelectListItem { Text = "Moldova (+373)", Value = "373" });
            model.Countries.Add(new SelectListItem { Text = "Monaco (+377)", Value = "377" });
            model.Countries.Add(new SelectListItem { Text = "Mongolia (+976)", Value = "976" });
            model.Countries.Add(new SelectListItem { Text = "Montserrat (+1664)", Value = "1664" });
            model.Countries.Add(new SelectListItem { Text = "Morocco (+212)", Value = "212" });
            model.Countries.Add(new SelectListItem { Text = "Mozambique (+258)", Value = "258" });
            model.Countries.Add(new SelectListItem { Text = "Myanmar (+95)", Value = "95" });
            model.Countries.Add(new SelectListItem { Text = "Namibia (+264)", Value = "264" });
            model.Countries.Add(new SelectListItem { Text = "Nauru (+674)", Value = "674" });
            model.Countries.Add(new SelectListItem { Text = "Nepal (+977)", Value = "977" });
            model.Countries.Add(new SelectListItem { Text = "Netherlands (+31)", Value = "31" });
            model.Countries.Add(new SelectListItem { Text = "New Caledonia (+687)", Value = "687" });
            model.Countries.Add(new SelectListItem { Text = "New Zealand (+64)", Value = "64" });
            model.Countries.Add(new SelectListItem { Text = "Nicaragua (+505)", Value = "505" });
            model.Countries.Add(new SelectListItem { Text = "Niger (+227)", Value = "227" });
            model.Countries.Add(new SelectListItem { Text = "Nigeria (+234)", Value = "234" });
            model.Countries.Add(new SelectListItem { Text = "Niue (+683)", Value = "683" });
            model.Countries.Add(new SelectListItem { Text = "Norfolk Islands (+672)", Value = "672" });
            model.Countries.Add(new SelectListItem { Text = "Northern Marianas (+670)", Value = "670" });
            model.Countries.Add(new SelectListItem { Text = "Norway (+47)", Value = "47" });
            model.Countries.Add(new SelectListItem { Text = "Oman (+968)", Value = "968" });
            model.Countries.Add(new SelectListItem { Text = "Palau (+680)", Value = "680" });
            model.Countries.Add(new SelectListItem { Text = "Panama (+507)", Value = "507" });
            model.Countries.Add(new SelectListItem { Text = "Papua New Guinea (+675)", Value = "675" });
            model.Countries.Add(new SelectListItem { Text = "Paraguay (+595)", Value = "595" });
            model.Countries.Add(new SelectListItem { Text = "Peru (+51)", Value = "51" });
            model.Countries.Add(new SelectListItem { Text = "Philippines (+63)", Value = "63" });
            model.Countries.Add(new SelectListItem { Text = "Poland (+48)", Value = "48" });
            model.Countries.Add(new SelectListItem { Text = "Portugal (+351)", Value = "351" });
            model.Countries.Add(new SelectListItem { Text = "Puerto Rico (+1787)", Value = "1787" });
            model.Countries.Add(new SelectListItem { Text = "Qatar (+974)", Value = "974" });
            model.Countries.Add(new SelectListItem { Text = "Reunion (+262)", Value = "262" });
            model.Countries.Add(new SelectListItem { Text = "Romania (+40)", Value = "40" });
            model.Countries.Add(new SelectListItem { Text = "Russia (+7)", Value = "7" });
            model.Countries.Add(new SelectListItem { Text = "Rwanda (+250)", Value = "250" });
            model.Countries.Add(new SelectListItem { Text = "San Marino (+378)", Value = "378" });
            model.Countries.Add(new SelectListItem { Text = "Sao Tome & Principe (+239)", Value = "239" });
            model.Countries.Add(new SelectListItem { Text = "Saudi Arabia (+966)", Value = "966" });
            model.Countries.Add(new SelectListItem { Text = "Senegal (+221)", Value = "221" });
            model.Countries.Add(new SelectListItem { Text = "Serbia (+381)", Value = "381" });
            model.Countries.Add(new SelectListItem { Text = "Seychelles (+248)", Value = "248" });
            model.Countries.Add(new SelectListItem { Text = "Sierra Leone (+232)", Value = "232" });
            model.Countries.Add(new SelectListItem { Text = "Singapore (+65)", Value = "65" });
            model.Countries.Add(new SelectListItem { Text = "Slovak Republic (+421)", Value = "421" });
            model.Countries.Add(new SelectListItem { Text = "Slovenia (+386)", Value = "386" });
            model.Countries.Add(new SelectListItem { Text = "Solomon Islands (+677)", Value = "677" });
            model.Countries.Add(new SelectListItem { Text = "Somalia (+252)", Value = "252" });
            model.Countries.Add(new SelectListItem { Text = "South Africa (+27)", Value = "27" });
            model.Countries.Add(new SelectListItem { Text = "Spain (+34)", Value = "34" });
            model.Countries.Add(new SelectListItem { Text = "Sri Lanka (+94)", Value = "94" });
            model.Countries.Add(new SelectListItem { Text = "St. Helena (+290)", Value = "290" });
            model.Countries.Add(new SelectListItem { Text = "St. Kitts (+1869)", Value = "1869" });
            model.Countries.Add(new SelectListItem { Text = "St. Lucia (+1758)", Value = "1758" });
            model.Countries.Add(new SelectListItem { Text = "Sudan (+249)", Value = "249" });
            model.Countries.Add(new SelectListItem { Text = "Suriname (+597)", Value = "597" });
            model.Countries.Add(new SelectListItem { Text = "Swaziland (+268)", Value = "268" });
            model.Countries.Add(new SelectListItem { Text = "Sweden (+46)", Value = "46" });
            model.Countries.Add(new SelectListItem { Text = "Switzerland (+41)", Value = "41" });
            model.Countries.Add(new SelectListItem { Text = "Syria (+963)", Value = "963" });
            model.Countries.Add(new SelectListItem { Text = "Taiwan (+886)", Value = "886" });
            model.Countries.Add(new SelectListItem { Text = "Tajikstan (+7)", Value = "7" });
            model.Countries.Add(new SelectListItem { Text = "Thailand (+66)", Value = "66" });
            model.Countries.Add(new SelectListItem { Text = "Togo (+228)", Value = "228" });
            model.Countries.Add(new SelectListItem { Text = "Tonga (+676)", Value = "676" });
            model.Countries.Add(new SelectListItem { Text = "Trinidad & Tobago (+1868)", Value = "1868" });
            model.Countries.Add(new SelectListItem { Text = "Tunisia (+216)", Value = "216" });
            model.Countries.Add(new SelectListItem { Text = "Turkey (+90)", Value = "90" });
            model.Countries.Add(new SelectListItem { Text = "Turkmenistan (+7)", Value = "7" });
            model.Countries.Add(new SelectListItem { Text = "Turkmenistan (+993)", Value = "993" });
            model.Countries.Add(new SelectListItem { Text = "Turks & Caicos Islands (+1649)", Value = "1649" });
            model.Countries.Add(new SelectListItem { Text = "Tuvalu (+688)", Value = "688" });
            model.Countries.Add(new SelectListItem { Text = "Uganda (+256)", Value = "256" });
            model.Countries.Add(new SelectListItem { Text = "UK (+44)", Value = "44" });
            model.Countries.Add(new SelectListItem { Text = "Ukraine (+380)", Value = "380" });
            model.Countries.Add(new SelectListItem { Text = "United Arab Emirates (+971)", Value = "971" });
            model.Countries.Add(new SelectListItem { Text = "Uruguay (+598)", Value = "598" });
            model.Countries.Add(new SelectListItem { Text = "USA (+1)", Value = "1" });
            model.Countries.Add(new SelectListItem { Text = "Uzbekistan (+7)", Value = "7" });
            model.Countries.Add(new SelectListItem { Text = "Vanuatu (+678)", Value = "678" });
            model.Countries.Add(new SelectListItem { Text = "Vatican City (+379)", Value = "379" });
            model.Countries.Add(new SelectListItem { Text = "Venezuela (+58)", Value = "58" });
            model.Countries.Add(new SelectListItem { Text = "Vietnam (+84)", Value = "84" });
            model.Countries.Add(new SelectListItem { Text = "Virgin Islands - British (+1284)", Value = "84" });
            model.Countries.Add(new SelectListItem { Text = "Virgin Islands - US (+1340)", Value = "84" });
            model.Countries.Add(new SelectListItem { Text = "Wallis & Futuna (+681)", Value = "681" });
            model.Countries.Add(new SelectListItem { Text = "Yemen (North)(+969)", Value = "969" });
            model.Countries.Add(new SelectListItem { Text = "Yemen (South)(+967)", Value = "967" });
            model.Countries.Add(new SelectListItem { Text = "Zambia (+260)", Value = "260" });
            model.Countries.Add(new SelectListItem { Text = "Zimbabwe (+263)", Value = "263" });
            //set postback values
            var form = this.Request.Form;
            model.PhoneNumber = form["PhoneNumber"];
            model.Country = form["Country"];
            var selectedCountry = model.Countries.FirstOrDefault(x => x.Value.Equals(form["Countries"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedCountry != null)
                selectedCountry.Selected = true;
            
            return View("~/Plugins/Payments.SendSpendManual/Views/PaymentInfo.cshtml", model);
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel
            {
                Country = form["Country"],
                PhoneNumber = form["PhoneNumber"]
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    warnings.Add(error.ErrorMessage);
            else {
                var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                var manualPaymentSettings = _settingService.LoadSetting<SendSpendManualPaymentSettings>(storeScope);

                using (var client = new HttpClient())
                {
                    var oAuth = new Models.ApiAuthRequest { apiId = manualPaymentSettings.AppId };
                    if (model.PhoneNumber.ToString().Substring(0, 2) == "00")
                    {
                        oAuth.customer_fmm_UserNameOrMobileNo = string.Format("+{0}", model.PhoneNumber.ToString().Substring(2, model.PhoneNumber.ToString().Length - 1));
                    }
                    else if (model.PhoneNumber.ToString().Substring(0, 1) == "0")
                    {
                        oAuth.customer_fmm_UserNameOrMobileNo = string.Format("+{0}{1}", model.Country, model.PhoneNumber.ToString().Substring(1, model.PhoneNumber.ToString().Length - 1));
                    }
                    var content = new StringContent(JsonConvert.SerializeObject(oAuth));
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    using (var res = client.PostAsync(String.Format("{0}api/v1/SecurePayment/Authentication", manualPaymentSettings.AppUrl), content))
                    {
                        res.Wait();
                        if (res.Result.IsSuccessStatusCode)
                        {
                            var data = res.Result.Content.ReadAsStringAsync();
                            var ret = JsonConvert.DeserializeObject<Models.ApiResponse>(data.Result.ToString());
                            switch (ret.responseCode.ToUpper())
                            {
                                case "APPROVED":
                                    break;
                                default:
                                    warnings.Add(ret.responseText);
                                    break;
                            }
                        }
                        else
                        {
                            warnings.Add("Unable to authenticate your request, Please try again later.");
                        }
                    }
                }
            }

            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            

            paymentInfo.CustomValues.Add("Currency", _workContext.WorkingCurrency.CurrencyCode);
            paymentInfo.CustomValues.Add("Country", form["Country"]);
            paymentInfo.CustomValues.Add("PhoneNumber", form["PhoneNumber"]);
            return paymentInfo;
        }

        [ValidateInput(false)]
        [AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
        public ActionResult IPNHandler(string order, string status)
        {
            var ret = "OK";
            try
            {
                var orderInfo = _orderService.GetOrderByGuid(new Guid(order));
                if (orderInfo != null)
                {
                    switch (status.ToUpper())
                    {
                        case "APPROVED":
                            orderInfo.OrderStatus = Core.Domain.Orders.OrderStatus.Processing;
                            orderInfo.PaymentStatus = Core.Domain.Payments.PaymentStatus.Paid;
                            break;
                        default:
                            orderInfo.OrderStatus = Core.Domain.Orders.OrderStatus.Cancelled;
                            orderInfo.PaymentStatus = Core.Domain.Payments.PaymentStatus.Voided;
                            break;
                    }
                    _orderService.UpdateOrder(orderInfo);
                }
                else
                {
                    ret = "ERROR";
                }
            }
            catch (Exception ex) {
                ret = "ERROR";
            }
            return Content(ret);
        }
    }
}