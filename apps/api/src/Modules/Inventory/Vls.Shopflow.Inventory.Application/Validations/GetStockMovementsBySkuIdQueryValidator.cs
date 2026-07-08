using FluentValidation;
using Vls.Shopflow.Inventory.Application.Queries;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class GetStockMovementsBySkuIdQueryValidator : AbstractValidator<GetStockMovementsBySkuIdQuery>
{
    public GetStockMovementsBySkuIdQueryValidator()
    {
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
