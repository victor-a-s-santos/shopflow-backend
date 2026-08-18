using FluentValidation;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Validators;

public sealed class GetAdminOrdersQueryValidator : AbstractValidator<GetAdminOrdersQuery>
{
    public const int MaxPageSize = 100;

    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt_desc",
        "createdAt_asc"
    };

    public GetAdminOrdersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);

        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<OrderStatus>(s!.Trim(), ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("status must be a valid OrderStatus (PendingPayment, Paid, Canceled, Expired).");

        RuleFor(x => x.FulfillmentStatus)
            .Must(s => Enum.TryParse<FulfillmentStatus>(s!.Trim(), ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.FulfillmentStatus))
            .WithMessage("fulfillmentStatus must be AwaitingShipment, Shipped, or Delivered.");

        // Payment status names come from PixPaymentStatus; validator references enum names as strings
        // to avoid a hard project reference — keep in sync with PaymentsPix.Domain.Enums.PixPaymentStatus.
        RuleFor(x => x.PaymentStatus)
            .Must(s => IsValidPaymentStatus(s!))
            .When(x => !string.IsNullOrWhiteSpace(x.PaymentStatus))
            .WithMessage("paymentStatus must be a valid PixPaymentStatus (Pending, Paid, Canceled, Expired, Failed).");

        RuleFor(x => x.Sort)
            .Must(s => AllowedSorts.Contains(s!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.Sort))
            .WithMessage("sort must be createdAt_desc or createdAt_asc.");

        RuleFor(x => x)
            .Must(x => x.CreatedFrom is null || x.CreatedTo is null || x.CreatedFrom <= x.CreatedTo)
            .WithMessage("createdFrom must be less than or equal to createdTo.");
    }

    private static bool IsValidPaymentStatus(string value)
        => string.Equals(value.Trim(), "Pending", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value.Trim(), "Paid", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value.Trim(), "Canceled", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value.Trim(), "Expired", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value.Trim(), "Failed", StringComparison.OrdinalIgnoreCase);
}

public sealed class GetAdminOrderByIdQueryValidator : AbstractValidator<GetAdminOrderByIdQuery>
{
    public GetAdminOrderByIdQueryValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
