using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Validations.Common;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(CommonRules.MaxNameLen);
        RuleFor(x => x.Slug)
            .SetValidator(new SlugValidator());
        RuleFor(x => x.CategoryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("CategoryId inválido.");
    }
}
