namespace Vls.Shopflow.Catalog.Application.Interfaces;

public interface IAttributeDefinitionLookup
{
    Task<AttributeDefinitionSnapshot?> GetByIdAsync(Guid definitionId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, AttributeDefinitionSnapshot>> GetByIdsAsync(
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken cancellationToken);
}

public sealed record AttributeDefinitionSnapshot(
    Guid Id,
    string Name,
    bool AllowCustomValues,
    IReadOnlyDictionary<Guid, AttributeValueSnapshot> Values);

public sealed record AttributeValueSnapshot(Guid Id, string Name);
