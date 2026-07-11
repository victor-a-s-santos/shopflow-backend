using FluentValidation;
using Vls.Shopflow.Inventory.Application.Queries;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class GetSkuAvailabilityBatchQueryValidator : AbstractValidator<GetSkuAvailabilityBatchQuery>
{
    public const int MaxSkuIds = 100;

    public GetSkuAvailabilityBatchQueryValidator()
    {
        RuleFor(x => x.SkuIds)
            .NotNull()
            .Must(ids => ids.Count > 0)
            .WithMessage("At least one skuId is required.")
            .Must(ids => ids.Count <= MaxSkuIds)
            .WithMessage($"A maximum of {MaxSkuIds} skuIds is allowed per request.");

        RuleForEach(x => x.SkuIds)
            .NotEmpty()
            .WithMessage("skuId must be a non-empty GUID.");
    }
}
