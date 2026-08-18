using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Exceptions;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class DeleteProductImageCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork unitOfWork,
    IImageStorage imageStorage,
    ILogger<DeleteProductImageCommandHandler> logger)
    : IRequestHandler<DeleteProductImageCommand>
{
    public async Task Handle(DeleteProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found.");

        var image = product.Images.FirstOrDefault(i => i.Id == request.ImageId);
        if (image is null)
        {
            throw new CatalogConflictException(
                "A imagem informada não pertence a este produto.",
                CatalogErrorCodes.ProductImageNotFound,
                "imageId");
        }

        var storageKey = image.ObjectKey;
        var storageProvider = image.StorageProvider;

        product.RemoveImage(request.ImageId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(storageKey))
        {
            try
            {
                await imageStorage.TryDeleteAsync(storageKey, storageProvider, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Product image DB row removed but storage delete failed for {ImageId} key {Key}",
                    request.ImageId,
                    storageKey);
            }
        }
    }
}

public sealed class SetPrimaryProductImageCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork unitOfWork)
    : IRequestHandler<SetPrimaryProductImageCommand>
{
    public async Task Handle(SetPrimaryProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found.");

        if (product.Images.All(i => i.Id != request.ImageId))
        {
            throw new CatalogConflictException(
                "A imagem informada não pertence a este produto.",
                CatalogErrorCodes.ProductImageNotFound,
                "primaryImageId");
        }

        product.PromoteImageToPrimary(request.ImageId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
