using FluentValidation;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.Services;

namespace Vls.Shopflow.Orders.Application.Validators;

public sealed class GetCustomerOrdersQueryValidator : AbstractValidator<GetCustomerOrdersQuery>
{
    public const int MaxPageSize = 50;

    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt_desc",
        "createdAt_asc"
    };

    public GetCustomerOrdersQueryValidator()
    {
        RuleFor(x => x.CustomerUserId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);

        RuleFor(x => x.Status)
            .Must(s => OrderCustomerStatusProjector.TryParseListFilter(s, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage(
                "status must be a public customerStatus (AwaitingPayment, Confirmed, Canceled, Expired) " +
                "or OrderStatus (PendingPayment, Paid, Canceled, Expired).");

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

public sealed class GetCustomerOrderByIdQueryValidator : AbstractValidator<GetCustomerOrderByIdQuery>
{
    public GetCustomerOrderByIdQueryValidator()
    {
        RuleFor(x => x.CustomerUserId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
