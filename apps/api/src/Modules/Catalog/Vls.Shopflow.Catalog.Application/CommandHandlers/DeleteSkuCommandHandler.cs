using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class DeleteSkuCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork uow)
    : IRequestHandler<DeleteSkuCommand>
{
    public async Task Handle(DeleteSkuCommand cmd, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(cmd.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        if (product.GetSku(cmd.SkuId) is null)
            throw new KeyNotFoundException("SKU not found.");

        product.RemoveSku(cmd.SkuId);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
