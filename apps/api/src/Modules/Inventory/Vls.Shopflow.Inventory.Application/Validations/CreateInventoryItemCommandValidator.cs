using FluentValidation;
using Vls.Shopflow.Inventory.Application.Commands;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemCommandValidator()
    {
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.InitialQuantity).GreaterThanOrEqualTo(0);
    }
}
