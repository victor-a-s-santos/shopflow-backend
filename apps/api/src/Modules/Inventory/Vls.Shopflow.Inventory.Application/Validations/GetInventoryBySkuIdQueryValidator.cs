using FluentValidation;
using Vls.Shopflow.Inventory.Application.Queries;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class GetInventoryBySkuIdQueryValidator : AbstractValidator<GetInventoryBySkuIdQuery>
{
    public GetInventoryBySkuIdQueryValidator()
    {
        RuleFor(x => x.SkuId).NotEmpty();
    }
}
