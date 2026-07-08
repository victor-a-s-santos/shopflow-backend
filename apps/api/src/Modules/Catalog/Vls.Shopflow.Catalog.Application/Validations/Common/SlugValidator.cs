using FluentValidation;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Application.Validations.Common;

public sealed class SlugValidator : AbstractValidator<string?>
{
    public SlugValidator()
    {
        When(s => !string.IsNullOrWhiteSpace(s), () =>
        {
            RuleFor(s => s!)
                .MaximumLength(CommonRules.MaxSlugLen)
                .Matches(CommonRules.SlugRegex)
                .WithMessage("Slug inválido. Use letras minúsculas, números e traços.");
        });
    }
}