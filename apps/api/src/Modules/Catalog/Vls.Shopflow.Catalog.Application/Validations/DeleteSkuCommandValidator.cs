using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class DeleteSkuCommandValidator : AbstractValidator<DeleteSkuCommand>
{
    public DeleteSkuCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
    }
}
