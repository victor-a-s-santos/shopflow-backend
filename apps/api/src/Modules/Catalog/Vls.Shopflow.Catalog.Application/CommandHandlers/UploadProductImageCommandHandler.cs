using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class UploadProductImageCommandHandler(
    IProductRepository productRepository,
    IImageStorage imageStorage,
    ICatalogUnitOfWork unitOfWork)
    : IRequestHandler<UploadProductImageCommand, ProductImageDto>
{
    public async Task<ProductImageDto> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found.");

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new ValidationException([
                new ValidationFailure(nameof(request.FileName), "Only PNG, JPEG, or WEBP files are allowed.")
            ]);

        var sortOrder = product.Images.Count == 0
            ? 0
            : product.Images.Max(i => i.SortOrder) + 1;

        var stored = await imageStorage.SaveAsync(
            product.Id,
            request.Content,
            request.FileName,
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
}
