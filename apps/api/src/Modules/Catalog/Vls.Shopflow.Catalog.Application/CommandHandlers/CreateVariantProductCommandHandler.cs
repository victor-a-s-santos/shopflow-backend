using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class CreateVariantProductCommandHandler(IProductRepository productRepository, ISlugService slugService, ICatalogUnitOfWork catalogUnitOfWork)
    : IRequestHandler<CreateVariantProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateVariantProductCommand cmd, CancellationToken cancellationToken)
    {
        var candidate = string.IsNullOrWhiteSpace(cmd.Slug)
            ? Slug.CreateFromName(cmd.Name)
            : Slug.From(cmd.Slug!);

        var slug = await slugService.EnsureUniqueAsync(candidate, cancellationToken);
        
        var product = Product.CreateWithSkus(
            cmd.Name,
            slug,
            cmd.CategoryId,
            cmd.IsFeatured,
            cmd.DisplayOrder,
            cmd.Description,
            isActive: cmd.IsActive ?? true);
        
        await productRepository.AddAsync(product, cancellationToken);
        await catalogUnitOfWork.SaveChangesAsync(cancellationToken);
        
        return product.Id;
    }
}