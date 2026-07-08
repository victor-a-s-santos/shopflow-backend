using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Application.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);
    Task AddImageAsync(ProductImage image, CancellationToken ct);
    void Delete(Product product);
}