using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Infrastructure.Repositories;

public sealed class AttributeDefinitionReadModel(CatalogDbContext db)
    : IAttributeDefinitionReadModel
{
    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var defs = await db.AttributeDefinitions
            .AsNoTracking()
            .Include(d => d.Values)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        return defs
            .Select(d => new AttributeDefinitionDto(
                d.Id,
                d.Name,
                d.AllowCustomValues,
                d.CategoryId,
                d.Values
                    .Select(v => new AttributeValueDefinitionDto(
                        v.Id,
                        v.Name,
                        v.HexColor
                    ))
                    .ToList()
            ))
            .ToList();
    }
}