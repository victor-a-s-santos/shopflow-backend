using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Infrastructure.Repositories;

public sealed class CategoryReadModel(CatalogDbContext db) : ICategoryReadModel
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug.Value))
            .ToListAsync(ct);
    }
}
