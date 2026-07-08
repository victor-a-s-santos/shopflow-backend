using FluentValidation;
using Vls.Shopflow.Inventory.Application.Queries;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class GetSkuAvailabilityBySkuIdQueryValidator : AbstractValidator<GetSkuAvailabilityBySkuIdQuery>
{
    public GetSkuAvailabilityBySkuIdQueryValidator()
    {
        RuleFor(x => x.SkuId).NotEmpty();
    }
}
