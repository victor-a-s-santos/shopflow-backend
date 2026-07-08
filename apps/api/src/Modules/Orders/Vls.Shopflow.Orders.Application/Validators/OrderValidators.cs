using FluentValidation;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Validators;

public sealed class CreateOrderFromCheckoutSessionCommandValidator
    : AbstractValidator<CreateOrderFromCheckoutSessionCommand>
{
    public CreateOrderFromCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.CheckoutSessionId).NotEmpty();
    }
}

public sealed class CreateOrderFromCheckoutSessionRequestValidator
    : AbstractValidator<CreateOrderFromCheckoutSessionRequest>
{
    public CreateOrderFromCheckoutSessionRequestValidator()
    {
        RuleFor(x => x.CheckoutSessionId).NotEmpty();
    }
}

public sealed class GetOrderByIdQueryValidator : AbstractValidator<GetOrderByIdQuery>
{
    public GetOrderByIdQueryValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetOrderByCheckoutSessionIdQueryValidator
    : AbstractValidator<GetOrderByCheckoutSessionIdQuery>
{
    public GetOrderByCheckoutSessionIdQueryValidator()
    {
        RuleFor(x => x.CheckoutSessionId).NotEmpty();
    }
}
