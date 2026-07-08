using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
