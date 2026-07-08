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
            .GreaterThan(0m);

        RuleFor(x => x.PromotionalPrice)
            .GreaterThanOrEqualTo(0m)
            .When(x => x.PromotionalPrice.HasValue);

        RuleFor(x => x)
            .Must(x => !x.PromotionalPrice.HasValue || x.PromotionalPrice.Value <= x.RegularPrice)
            .WithMessage("PromotionalPrice deve ser <= RegularPrice.");

        // Lista de atributos
        RuleFor(x => x.Attributes)
            .NotNull()
            .WithMessage("A lista de atributos não pode ser nula.");

        RuleForEach(x => x.Attributes!).SetValidator(new SkuAttributeCreateDtoValidator());
    }
}