using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;


public sealed class DeactivateProductCommandHandler(
    IProductRepository repository,
    ICatalogUnitOfWork uow
) : IRequestHandler<DeactivateProductCommand>
{
    public async Task Handle(DeactivateProductCommand cmd, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(cmd.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found.");

        product.Deactivate();

        await uow.SaveChangesAsync(cancellationToken);
    }
}