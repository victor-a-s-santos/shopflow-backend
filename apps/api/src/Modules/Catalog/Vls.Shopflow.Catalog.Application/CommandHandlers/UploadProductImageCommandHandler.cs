using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.Exceptions;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class UploadProductImageCommandHandler(
    IProductRepository productRepository,
    IImageStorage imageStorage,
    ICatalogUnitOfWork unitOfWork)
    : IRequestHandler<UploadProductImageCommand, ProductImageDto>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp"
    };

    public async Task<ProductImageDto> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found.");

        if (product.Images.Count >= Product.MaxImages)
        {
            throw new CatalogConflictException(
                $"O produto já possui o máximo de {Product.MaxImages} imagens.",
                CatalogErrorCodes.ProductImageLimit,
                "images");
        }

        var failures = new List<ValidationFailure>();

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
        {
            failures.Add(new ValidationFailure(
                "file",
                "Apenas arquivos PNG, JPEG ou WEBP são permitidos."));
        }

        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            failures.Add(new ValidationFailure(
                "file",
                "Content-Type inválido. Use image/png, image/jpeg ou image/webp."));
        }

        if (!LooksLikeAllowedImage(request.Content, ext, out var sniffedExt))
        {
            failures.Add(new ValidationFailure(
                "file",
                "O conteúdo do arquivo não corresponde a uma imagem PNG, JPEG ou WEBP válida."));
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        // Rewind if the sniff advanced the stream
        if (request.Content.CanSeek)
            request.Content.Position = 0;

        var sortOrder = product.Images.Count == 0
            ? 0
            : product.Images.Max(i => i.SortOrder) + 1;

        var fileName = string.IsNullOrWhiteSpace(Path.GetExtension(request.FileName)) && sniffedExt is not null
            ? request.FileName + sniffedExt
            : request.FileName;

        var stored = await imageStorage.SaveAsync(
            product.Id,
            request.Content,
            fileName,
            request.ContentType,
            cancellationToken);

        var image = ProductImage.Create(
            product.Id,
            stored.Url,
            stored.StoragePath,
            sortOrder,
            isPrimary: false);

        product.AddImage(image);

        await productRepository.AddImageAsync(image, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProductImageDto(image.Id, image.Url, image.SortOrder, image.IsPrimary);
    }

    private static bool LooksLikeAllowedImage(Stream content, string declaredExt, out string? sniffedExt)
    {
        sniffedExt = null;
        Span<byte> header = stackalloc byte[12];
        var read = content.Read(header);
        if (content.CanSeek)
            content.Position = 0;

        if (read < 3)
            return false;

        // PNG
        if (read >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            sniffedExt = ".png";
            return declaredExt is "" or ".png";
        }

        // JPEG
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            sniffedExt = ".jpg";
            return declaredExt is "" or ".jpg" or ".jpeg";
        }

        // WEBP: RIFF....WEBP
        if (read >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
        {
            sniffedExt = ".webp";
            return declaredExt is "" or ".webp";
        }

        return false;
    }
}
