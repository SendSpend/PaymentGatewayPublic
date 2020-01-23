using FluentValidation;
using Nop.Plugin.Payments.SendSpendManual.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.SendSpendManual.Validators
{
    public partial class PaymentInfoValidator : BaseNopValidator<PaymentInfoModel>
    {
        public PaymentInfoValidator(ILocalizationService localizationService)
        {
            RuleFor(x => x.Country).NotEmpty().WithMessage(localizationService.GetResource("Plugins.Payments.FMMManual.Country.Required"));
            RuleFor(x => x.PhoneNumber).Matches(@"^[0-9]{4,15}$").WithMessage(localizationService.GetResource("Plugins.Payments.FMMManual.PhoneNumber.Wrong"));
        }
    }
}