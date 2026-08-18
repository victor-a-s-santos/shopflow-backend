using FluentValidation;
using Vls.Shopflow.Inventory.Application.Commands;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class RemoveStockCommandValidator : AbstractValidator<RemoveStockCommand>
{
    public RemoveStockCommandValidator()
    {
        RuleFor(x => x.SkuId)
            .NotEmpty()
            .WithName("skuId");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("A quantidade da baixa deve ser maior que zero.")
            .WithName("quantity");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("O motivo da baixa é obrigatório.")
            .MaximumLength(500)
            .WithMessage("O motivo da baixa deve ter no máximo 500 caracteres.")
            .WithName("reason");
    }
}
