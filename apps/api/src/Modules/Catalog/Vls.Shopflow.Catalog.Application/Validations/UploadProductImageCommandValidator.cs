using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class UploadProductImageCommandValidator : AbstractValidator<UploadProductImageCommand>
{
    public UploadProductImageCommandValidator()
    {
        RuleFor(x => x.Length)
            .InclusiveBetween(1, 5 * 1024 * 1024)
            .WithMessage("Image must be between 1 byte and 5 MB.");
    }
}
