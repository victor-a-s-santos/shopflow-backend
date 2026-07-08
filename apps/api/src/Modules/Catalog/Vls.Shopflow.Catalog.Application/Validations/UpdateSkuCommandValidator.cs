using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Validations.Common;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class UpdateSkuCommandValidator : AbstractValidator<UpdateSkuCommand>
{
    public UpdateSkuCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Code)
            .MaximumLength(CommonRules.MaxSkuCodeLen)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));
        RuleFor(x => x.RegularPrice).GreaterThan(0m);
        RuleFor(x => x.PromotionalPrice)
            .GreaterThanOrEqualTo(0m)
            .When(x => x.PromotionalPrice.HasValue);
        RuleFor(x => x)
            .Must(x => !x.PromotionalPrice.HasValue || x.PromotionalPrice.Value <= x.RegularPrice)
            .WithMessage("PromotionalPrice deve ser <= RegularPrice.");
        RuleFor(x => x.Attributes).NotNull().WithMessage("A lista de atributos não pode ser nula.");
        RuleForEach(x => x.Attributes!).SetValidator(new SkuAttributeCreateDtoValidator());
        RuleFor(x => x.Attributes)
            .Must(HaveUniqueGlobalDefinitionIds)
            .When(x => x.Attributes is { Count: > 0 })
            .WithMessage("A SKU cannot have more than one value for the same global attribute.");
    }

    private static bool HaveUniqueGlobalDefinitionIds(
        IReadOnlyList<DataTransferObjects.SkuAttributeCreateDto>? attributes)
    {
        if (attributes is null) return true;
        var seen = new HashSet<Guid>();
        foreach (var attr in attributes)
        {
            if (attr.AttributeDefinitionId is not { } definitionId)
                continue;
            if (!seen.Add(definitionId))
                return false;
        }
        return true;
    }
}
