using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Infrastructure.Repositories;

public sealed class AttributeDefinitionLookup(CatalogDbContext db) : IAttributeDefinitionLookup
{
    public async Task<AttributeDefinitionSnapshot?> GetByIdAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var map = await GetByIdsAsync([definitionId], cancellationToken);
        return map.TryGetValue(definitionId, out var snapshot) ? snapshot : null;
    }

    public async Task<IReadOnlyDictionary<Guid, AttributeDefinitionSnapshot>> GetByIdsAsync(
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken cancellationToken)
    {
        if (definitionIds.Count == 0)
            return new Dictionary<Guid, AttributeDefinitionSnapshot>();

        var ids = definitionIds.Distinct().ToList();
        var defs = await db.AttributeDefinitions
            .AsNoTracking()
            .Include(d => d.Values)
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(cancellationToken);

        return defs.ToDictionary(
            d => d.Id,
            d => new AttributeDefinitionSnapshot(
                d.Id,
                d.Name,
                d.AllowCustomValues,
                d.Values.ToDictionary(
                    v => v.Id,
                    v => new AttributeValueSnapshot(v.Id, v.Name))));
    }
}
