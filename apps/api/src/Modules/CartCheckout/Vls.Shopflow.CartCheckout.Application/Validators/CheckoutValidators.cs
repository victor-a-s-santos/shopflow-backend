using FluentValidation;
using Vls.Shopflow.CartCheckout.Application.Commands;

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
    }
}

public sealed class CancelCheckoutSessionCommandValidator : AbstractValidator<CancelCheckoutSessionCommand>
{
    public CancelCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.CheckoutSessionId).NotEmpty();
    }
}
