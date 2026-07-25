using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Validations.Common;

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
    }
}