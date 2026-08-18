using FluentValidation;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.CartCheckout.Domain.Services;

namespace Vls.Shopflow.CartCheckout.Application.Validators;

public sealed class CreateCheckoutSessionCommandValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.Customer).NotNull();
        RuleFor(x => x.Customer.FullName).NotEmpty().When(x => x.Customer is not null);
        RuleFor(x => x.Customer.Email).NotEmpty().EmailAddress().When(x => x.Customer is not null);
        RuleFor(x => x.Customer.Phone).NotEmpty().When(x => x.Customer is not null);

        RuleFor(x => x.Address).NotNull();
        RuleFor(x => x.Address.ZipCode).NotEmpty().When(x => x.Address is not null);
        RuleFor(x => x.Address.Street).NotEmpty().When(x => x.Address is not null);
        RuleFor(x => x.Address.Number).NotEmpty().When(x => x.Address is not null);
        RuleFor(x => x.Address.Neighborhood).NotEmpty().When(x => x.Address is not null);
        RuleFor(x => x.Address.City).NotEmpty().When(x => x.Address is not null);
        RuleFor(x => x.Address.State).NotEmpty().When(x => x.Address is not null);

        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SkuId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });

        RuleFor(x => x.PreferredDeliveryMethod)
            .Must(s => Enum.TryParse<DeliveryMethod>(s!.Trim(), ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredDeliveryMethod))
            .WithErrorCode("INVALID_DELIVERY_METHOD")
            .WithMessage("preferredDeliveryMethod must be Carrier, ExcursionBus, or Correios.");

        RuleFor(x => x.PreferredDeliveryDate)
            .Must(d => d is null
                       || DeliveryDatePolicy.IsValidPreferredDeliveryDate(
                           DateOnly.FromDateTime(DateTime.UtcNow),
                           d.Value))
            .WithErrorCode(DeliveryDatePolicy.DeliveryDateTooSoonCode)
            .WithMessage(DeliveryDatePolicy.DeliveryDateTooSoonMessage);

        RuleFor(x => x.CustomerOrderNote)
            .MaximumLength(CheckoutSession.CustomerOrderNoteMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerOrderNote))
            .WithErrorCode("CUSTOMER_ORDER_NOTE_TOO_LONG")
            .WithMessage("A observação do cliente deve ter no máximo 1000 caracteres.");
    }
}

public sealed class CancelCheckoutSessionCommandValidator : AbstractValidator<CancelCheckoutSessionCommand>
{
    public CancelCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.CheckoutSessionId).NotEmpty();
    }
}
