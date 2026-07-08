using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class DeleteProductCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork uow)
    : IRequestHandler<DeleteProductCommand>
{
    public async Task Handle(DeleteProductCommand cmd, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(cmd.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        productRepository.Delete(product);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
