using FluentValidation;
using Vls.Shopflow.Catalog.Application.Queries;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}