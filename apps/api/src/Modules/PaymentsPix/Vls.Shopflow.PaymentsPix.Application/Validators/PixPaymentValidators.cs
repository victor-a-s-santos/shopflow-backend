using FluentValidation;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Queries;

namespace Vls.Shopflow.PaymentsPix.Application.Validators;

public sealed class CreatePixPaymentForOrderCommandValidator
    : AbstractValidator<CreatePixPaymentForOrderCommand>
{
    public CreatePixPaymentForOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order id is required.");
    }
}

public sealed class GetPixPaymentByIdQueryValidator : AbstractValidator<GetPixPaymentByIdQuery>
{
    public GetPixPaymentByIdQueryValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty()
            .WithMessage("Payment id is required.");
    }
}

public sealed class GetPixPaymentByOrderIdQueryValidator : AbstractValidator<GetPixPaymentByOrderIdQuery>
{
    public GetPixPaymentByOrderIdQueryValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order id is required.");
    }
}
