using MediatR;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Services;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class UpdateSkuCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork uow)
    : IRequestHandler<UpdateSkuCommand>
{
    public async Task Handle(UpdateSkuCommand cmd, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(cmd.ProductId, cancellationToken);
        if (product is null)
            throw new KeyNotFoundException("Product not found.");

        var sku = product.GetSku(cmd.SkuId);
        if (sku is null)
            throw new KeyNotFoundException("SKU not found.");

        var newAttributes = SkuAttributeFactory.CreateFromDtos(cmd.Attributes);

        sku.ChangeCode(cmd.Code);
        sku.ChangePrice(Price.From(cmd.RegularPrice, cmd.PromotionalPrice));
        sku.ReplaceAttributes(newAttributes);

        if (cmd.Active)
            sku.Activate();
        else
            sku.Deactivate();

        await uow.SaveChangesAsync(cancellationToken);
    }
}
