using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Validations.Common;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class CreateVariantProductValidator : AbstractValidator<CreateVariantProductCommand>
{
    public CreateVariantProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(CommonRules.MaxNameLen);

        RuleFor(x => x.Slug)
            .SetValidator(new SlugValidator());

        RuleFor(x => x.CategoryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("CategoryId inválido.");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DisplayOrder.HasValue)
            .WithMessage("DisplayOrder não pode ser negativo.");

        RuleFor(x => x.Description)
            .MaximumLength(Product.MaxDescriptionLength)
            .WithMessage($"A descrição não pode ter mais de {Product.MaxDescriptionLength} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}