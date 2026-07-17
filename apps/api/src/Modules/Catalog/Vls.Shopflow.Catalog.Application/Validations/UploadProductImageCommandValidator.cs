using FluentValidation;
using Vls.Shopflow.Catalog.Application.Commands;

namespace Vls.Shopflow.Catalog.Application.Validations;

public sealed class UploadProductImageCommandValidator : AbstractValidator<UploadProductImageCommand>
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public UploadProductImageCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().WithMessage("O nome do arquivo é obrigatório.");
        RuleFor(x => x.Length)
            .InclusiveBetween(1, MaxBytes)
            .WithMessage($"A imagem deve ter entre 1 byte e {MaxBytes / (1024 * 1024)} MB.")
            .WithName("file");
    }
}

public sealed class DeleteProductImageCommandValidator : AbstractValidator<DeleteProductImageCommand>
{
    public DeleteProductImageCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ImageId).NotEmpty().WithMessage("O id da imagem é obrigatório.");
    }
}

public sealed class SetPrimaryProductImageCommandValidator : AbstractValidator<SetPrimaryProductImageCommand>
{
    public SetPrimaryProductImageCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ImageId).NotEmpty().WithMessage("O id da imagem principal é obrigatório.");
    }
}
