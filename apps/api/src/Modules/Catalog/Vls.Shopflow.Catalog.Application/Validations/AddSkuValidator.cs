using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Validations.Common;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class AddSkuValidator : AbstractValidator<AddSkuCommand>
{
    public AddSkuValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();

        RuleFor(x => x.Code)
            .MaximumLength(CommonRules.MaxSkuCodeLen)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.RegularPrice)
            .GreaterThan(0m)
            .WithMessage("O preço regular deve ser maior que zero.")
            .Must(MoneyRules.HasAtMostTwoDecimalPlaces)
            .WithMessage("O preço regular deve ter no máximo duas casas decimais.")
            .WithName("regularPrice");

        RuleFor(x => x.PromotionalPrice)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("O preço promocional não pode ser negativo.")
            .Must(v => v is null || MoneyRules.HasAtMostTwoDecimalPlaces(v.Value))
            .WithMessage("O preço promocional deve ter no máximo duas casas decimais.")
            .When(x => x.PromotionalPrice.HasValue)
            .WithName("promotionalPrice");

        RuleFor(x => x)
            .Must(x => !x.PromotionalPrice.HasValue || x.PromotionalPrice.Value < x.RegularPrice)
            .WithMessage("O preço promocional deve ser menor que o preço regular.")
            .WithName("promotionalPrice");

        RuleFor(x => x.Attributes)
            .NotNull()
            .WithMessage("A lista de atributos não pode ser nula.");

        RuleForEach(x => x.Attributes!).SetValidator(new SkuAttributeCreateDtoValidator());

        RuleFor(x => x.Attributes)
            .Must(HaveUniqueGlobalDefinitionIds)
            .When(x => x.Attributes is { Count: > 0 })
            .WithMessage("A SKU não pode ter mais de um valor para o mesmo atributo.")
            .WithName("attributes");
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
