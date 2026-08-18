using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Exceptions;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class DeleteSkuCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork uow,
    ISkuLifecycleGuard lifecycleGuard)
    : IRequestHandler<DeleteSkuCommand>
{
    public async Task Handle(DeleteSkuCommand cmd, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(cmd.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        if (product.GetSku(cmd.SkuId) is null)
            throw new KeyNotFoundException("SKU not found.");

        var protection = await lifecycleGuard.GetProtectionAsync(cmd.SkuId, cancellationToken);
        if (protection.BlocksHardDelete)
        {
            throw new CatalogConflictException(
                "Não é possível excluir uma SKU com estoque, reservas, movimentações ou histórico de pedidos. Inative a variação (active=false) em vez de excluí-la.",
                CatalogErrorCodes.SkuDeleteProtected,
                "id");
        }

        product.RemoveSku(cmd.SkuId);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
