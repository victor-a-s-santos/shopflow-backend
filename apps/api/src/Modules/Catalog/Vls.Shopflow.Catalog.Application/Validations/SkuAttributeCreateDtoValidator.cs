using FluentValidation;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Validations.Common;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class SkuAttributeCreateDtoValidator : AbstractValidator<SkuAttributeCreateDto>
{
    public SkuAttributeCreateDtoValidator()
    {
        RuleFor(x => x)
            .Must(x =>
                (x.AttributeDefinitionId.HasValue && x.AttributeValueDefinitionId.HasValue)
                ||
                (!string.IsNullOrWhiteSpace(x.CustomName) && !string.IsNullOrWhiteSpace(x.CustomValue))
            )
            .WithMessage("Atributo deve ser global (DefinitionId + ValueId) OU custom (CustomName + CustomValue).");

        When(x => !string.IsNullOrWhiteSpace(x.CustomName), () =>
        {
            RuleFor(x => x.CustomName!)
                .MaximumLength(CommonRules.MaxCustomNameLen);
        });

        When(x => !string.IsNullOrWhiteSpace(x.CustomValue), () =>
        {
            RuleFor(x => x.CustomValue!)
                .MaximumLength(CommonRules.MaxCustomValueLen);
        });
    }
}