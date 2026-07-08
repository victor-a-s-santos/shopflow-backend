using FluentValidation;
using Vls.Shopflow.Inventory.Application.Commands;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ExpiresAt)
            .Must(d => !d.HasValue || d.Value > DateTimeOffset.UtcNow)
            .WithMessage("ExpiresAt must be in the future.")
            .When(x => x.ExpiresAt.HasValue);
    }
}
