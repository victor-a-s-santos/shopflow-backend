using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Infrastructure.Repositories;

public sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Products
            .AsSplitQuery()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Skus)
                .ThenInclude(s => s.Attributes)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task AddAsync(Product product, CancellationToken ct)
        => db.Products.AddAsync(product, ct).AsTask();

    public Task AddImageAsync(ProductImage image, CancellationToken ct)
        => db.ProductImages.AddAsync(image, ct).AsTask();

    public void Delete(Product product) => db.Products.Remove(product);
}
