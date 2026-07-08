using MediatR;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class AddSkuCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork catalogUnitOfWork)
    : IRequestHandler<AddSkuCommand, Guid>
{
    public async Task<Guid> Handle(AddSkuCommand cmd, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(cmd.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found");

        var attributes = SkuAttributeFactory.CreateFromDtos(cmd.Attributes);
        var price = Price.From(cmd.RegularPrice, cmd.PromotionalPrice);

        var sku = Sku.Create(product.Id, cmd.Code, price, attributes, cmd.Active);
        product.AddSku(sku);

        await catalogUnitOfWork.SaveChangesAsync(cancellationToken);
        return sku.Id;
    }
}
