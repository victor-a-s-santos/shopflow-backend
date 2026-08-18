using FluentValidation;
using Vls.Shopflow.Catalog.Application.Queries;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public const int MaxSearchLength = 100;

    public GetProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 48);
        RuleFor(x => x.Sort)
            .Must(s => string.IsNullOrWhiteSpace(s) || ProductListSort.Allowed.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Sort must be one of: default, newest, price_asc, price_desc, name_asc.");
        RuleFor(x => x.CategorySlug)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.CategorySlug));
        RuleFor(x => x.CategoryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("CategoryId inválido.");
        RuleFor(x => x.Q)
            .MaximumLength(MaxSearchLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Q));
    }
}
