using Vls.Shopflow.BuildingBlocks.Domain.Entities;

namespace Vls.Shopflow.Catalog.Domain.Entities;

public class SkuAttribute : Entity<Guid>
{
    public Guid SkuId { get; internal set; }
    public Sku Sku { get; internal set; } = default!;

    public Guid? AttributeDefinitionId { get; private set; }
    public AttributeDefinition? AttributeDefinition { get; private set; }

    public Guid? AttributeValueDefinitionId { get; private set; }
    public AttributeValueDefinition? AttributeValueDefinition { get; private set; }

    public string? CustomName { get; private set; }
    public string? CustomValue { get; private set; }

    private SkuAttribute() { }

    public static SkuAttribute FromGlobal(Guid definitionId, Guid valueId)
        => new()
        {
            Id = Guid.NewGuid(),
            AttributeDefinitionId = definitionId,
            AttributeValueDefinitionId = valueId,
        };

    /// <summary>
    /// Custom value for a known attribute definition (AllowCustomValues = true).
    /// <paramref name="customName"/> is the free-text value (e.g. "Variadas").
    /// </summary>
    public static SkuAttribute FromDefinitionCustom(Guid definitionId, string customName)
        => new()
        {
            Id = Guid.NewGuid(),
            AttributeDefinitionId = definitionId,
            CustomName = customName,
        };

    /// <summary>
    /// Legacy free-form attribute without a global definition (tests / seed only).
    /// Prefer <see cref="FromDefinitionCustom"/> for admin API payloads.
    /// </summary>
    public static SkuAttribute FromCustom(string name, string value)
        => new()
        {
            Id = Guid.NewGuid(),
            CustomName = name,
            CustomValue = value,
        };
}
