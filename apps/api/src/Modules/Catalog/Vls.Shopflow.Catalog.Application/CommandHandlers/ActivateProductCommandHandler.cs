using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class ActivateProductCommandHandler(
    IProductRepository repository,
    ICatalogUnitOfWork uow
) : IRequestHandler<ActivateProductCommand>
{
    public async Task Handle(ActivateProductCommand cmd, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(cmd.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found.");

        product.Activate();

        await uow.SaveChangesAsync(cancellationToken);
    }
}