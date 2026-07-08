using FluentValidation;
using Vls.Shopflow.Inventory.Application.Commands;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class RemoveStockCommandValidator : AbstractValidator<RemoveStockCommand>
{
    public RemoveStockCommandValidator()
    {
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}
