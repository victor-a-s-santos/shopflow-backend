using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Exceptions;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class DeleteProductImageCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork unitOfWork)
    : IRequestHandler<DeleteProductImageCommand>
{
    public async Task Handle(DeleteProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found.");

        if (product.Images.All(i => i.Id != request.ImageId))
        {
            throw new CatalogConflictException(
                "A imagem informada não pertence a este produto.",
                CatalogErrorCodes.ProductImageNotFound,
                "imageId");
        }

        product.RemoveImage(request.ImageId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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
