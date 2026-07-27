using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class UpdateProductCommandHandler(
    IProductRepository productRepository,
    ISlugService slugService,
    ICatalogUnitOfWork uow)
    : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(UpdateProductCommand cmd, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(cmd.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        var slug = string.IsNullOrWhiteSpace(cmd.Slug)
            ? product.Slug
            : await slugService.EnsureUniqueAsync(Slug.From(cmd.Slug!), cmd.ProductId, cancellationToken);

        product.UpdateInfo(cmd.Name, slug, cmd.CategoryId, cmd.IsActive);

        if (cmd.UpdateDescription)
            product.ChangeDescription(cmd.Description);

        if (cmd.UpdateDisplaySettings)
            product.ChangeDisplaySettings(cmd.IsFeatured ?? false, cmd.DisplayOrder);

        await uow.SaveChangesAsync(cancellationToken);
    }
}
