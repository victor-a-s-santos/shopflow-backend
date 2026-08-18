using FluentValidation;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Validations.Common;

namespace Vls.Shopflow.Catalog.Application.Validations;

/// <summary>
/// Shape-only validation. Existence / ownership checks run in <see cref="Services.SkuAttributeFactory"/>.
/// </summary>
public sealed class SkuAttributeCreateDtoValidator : AbstractValidator<SkuAttributeCreateDto>
{
    public SkuAttributeCreateDtoValidator()
    {
        RuleFor(x => x.AttributeDefinitionId)
            .NotEmpty()
            .WithMessage("O atributo deve informar attributeDefinitionId.")
            .WithErrorCode("ATTRIBUTE_DEFINITION_REQUIRED");

        RuleFor(x => x)
            .Must(HaveExactlyOneValueSource)
            .WithMessage(
                "Informe attributeValueDefinitionId (valor predefinido) OU customName (valor personalizado), nunca ambos.")
            .WithErrorCode("ATTRIBUTE_VALUE_XOR");

        RuleFor(x => x)
            .Must(x => !(x.AttributeValueDefinitionId.HasValue
                         && x.AttributeValueDefinitionId != Guid.Empty
                         && !string.IsNullOrWhiteSpace(x.CustomName)))
            .WithMessage("Não informe customName junto com attributeValueDefinitionId.")
            .WithErrorCode("ATTRIBUTE_MIXED_VALUE");

        When(x => !string.IsNullOrWhiteSpace(x.CustomName), () =>
        {
            RuleFor(x => x.CustomName!)
                .MinimumLength(1)
                .MaximumLength(CommonRules.MaxCustomNameLen)
                .WithMessage($"O valor personalizado deve ter no máximo {CommonRules.MaxCustomNameLen} caracteres.");

            RuleFor(x => x.AttributeValueDefinitionId)
                .Must(id => id is null || id == Guid.Empty)
                .WithMessage("attributeValueDefinitionId deve ser nulo quando customName é informado.");
        });

        When(x => x.AttributeValueDefinitionId is { } id && id != Guid.Empty, () =>
        {
            RuleFor(x => x.CustomName)
                .Must(string.IsNullOrWhiteSpace)
                .WithMessage("customName deve estar vazio quando attributeValueDefinitionId é informado.");

            RuleFor(x => x.CustomValue)
                .Must(string.IsNullOrWhiteSpace)
                .WithMessage("customValue deve estar vazio quando attributeValueDefinitionId é informado.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.CustomValue), () =>
        {
            RuleFor(x => x.CustomValue!)
                .MaximumLength(CommonRules.MaxCustomValueLen);
        });
    }

    private static bool HaveExactlyOneValueSource(SkuAttributeCreateDto x)
    {
        var hasValueId = x.AttributeValueDefinitionId is { } id && id != Guid.Empty;
        var hasCustom = !string.IsNullOrWhiteSpace(x.CustomName);
        return hasValueId ^ hasCustom;
    }
}
