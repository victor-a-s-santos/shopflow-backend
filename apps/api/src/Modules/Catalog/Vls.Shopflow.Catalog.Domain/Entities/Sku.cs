using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Domain.Entities;

public sealed class Sku : Entity<Guid>
{
    private readonly List<SkuAttribute> _attributes = new();

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;

    public string Code { get; private set; } = default!;
    public Price Price { get; private set; } = Price.From(0);
    public SkuSalesRule SalesRule { get; private set; } = SkuSalesRule.UnitDefault();
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<SkuAttribute> Attributes => _attributes.AsReadOnly();

    private Sku() {}

    private Sku(
        Guid productId,
        string? code,
        Price price,
        IEnumerable<SkuAttribute>? attributes,
        bool active,
        SkuSalesRule? salesRule)
    {
        Id = Guid.NewGuid();
        ProductId = productId;

        Code = string.IsNullOrWhiteSpace(code)
            ? $"SKU-{Id.ToString("N")[..8].ToUpperInvariant()}"
            : code.Trim();

        Price = price.Normalize();
        SalesRule = salesRule ?? SkuSalesRule.UnitDefault();
        IsActive = active;

        if (attributes is not null)
            AddAttributes(attributes);
    }

    public static Sku Create(
        Guid productId,
        string? code,
        Price price,
        IEnumerable<SkuAttribute>? attributes,
        bool active,
        SkuSalesRule? salesRule = null)
        => new(productId, code, price, attributes, active, salesRule);

    public void AddAttribute(SkuAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        EnsureNoDuplicateGlobalDefinition(attribute);
        _attributes.Add(attribute);
    }

    public void ChangePrice(Price newPrice)
        => Price = newPrice.Normalize();

    public void ChangeSalesRule(SkuSalesRule salesRule)
    {
        ArgumentNullException.ThrowIfNull(salesRule);
        SalesRule = salesRule;
    }

    public void Activate()
        => IsActive = true;

    public void Deactivate()
        => IsActive = false;

    public void ChangeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
    }

    public void ClearAttributes()
    {
        while (_attributes.Count > 0)
            _attributes.RemoveAt(_attributes.Count - 1);
    }

    public void ReplaceAttributes(IEnumerable<SkuAttribute> newAttributes)
    {
        var incoming = (newAttributes ?? []).ToList();
        EnsureNoDuplicateGlobalDefinitions(incoming);

        ClearAttributes();

        foreach (var attribute in incoming)
            AddAttribute(attribute);
    }

    private void AddAttributes(IEnumerable<SkuAttribute> attributes)
    {
        foreach (var attribute in attributes)
            AddAttribute(attribute);
    }

    private void EnsureNoDuplicateGlobalDefinition(SkuAttribute attribute)
    {
        if (attribute.AttributeDefinitionId is not { } definitionId)
            return;

        if (_attributes.Any(a => a.AttributeDefinitionId == definitionId))
            throw new InvalidOperationException(
                "A SKU cannot have more than one value for the same global attribute.");
    }

    private static void EnsureNoDuplicateGlobalDefinitions(IReadOnlyList<SkuAttribute> attributes)
    {
        var seen = new HashSet<Guid>();
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeDefinitionId is not { } definitionId)
                continue;

            if (!seen.Add(definitionId))
                throw new InvalidOperationException(
                    "A SKU cannot have more than one value for the same global attribute.");
        }
    }
}
