using FluentValidation;
using Vls.Shopflow.Catalog.Application.Queries;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class GetProductByIdQueryValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}