using FluentValidation;
using Vls.Shopflow.Catalog.Application.Queries;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class GetAdminProductsQueryValidator : AbstractValidator<GetAdminProductsQuery>
{
    public const int MaxSearchLength = 100;

    public GetAdminProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Sort)
            .Must(s => string.IsNullOrWhiteSpace(s) || AdminProductListSort.Allowed.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage(
                "Sort must be one of: default, newest, oldest, name_asc, name_desc, display_order, featured, price_asc, price_desc.");
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || AdminProductListFilters.StatusAllowed.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Status must be one of: all, active, inactive.");
        RuleFor(x => x.Featured)
            .Must(s => string.IsNullOrWhiteSpace(s) || AdminProductListFilters.FeaturedAllowed.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Featured must be one of: all, featured, not_featured.");
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
