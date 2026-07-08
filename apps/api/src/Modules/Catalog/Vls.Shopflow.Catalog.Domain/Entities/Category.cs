using Vls.Shopflow.BuildingBlocks.Domain.Entities;

namespace Vls.Shopflow.Catalog.Domain.Entities;

public sealed class Category : Entity<Guid>
{
    public string Name { get; private set; } = default!;

    private readonly List<AttributeDefinition> _defaultAttributes = new();
    public IReadOnlyCollection<AttributeDefinition> DefaultAttributes => _defaultAttributes;
    
    private readonly List<Guid> _defaultAttributeDefinitionIds = new();
    
    public IReadOnlyList<Guid> DefaultAttributeDefinitions 
        => _defaultAttributeDefinitionIds.AsReadOnly();

    private Category() { }

    public Category(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
    }

    public void AddAttribute(AttributeDefinition attr)
    {
        _defaultAttributes.Add(attr);
    }
    
    public void AddDefaultAttribute(AttributeDefinition definition)
    {
        if (definition is null) 
            throw new ArgumentNullException(nameof(definition));

        if (!_defaultAttributeDefinitionIds.Contains(definition.Id))
            _defaultAttributeDefinitionIds.Add(definition.Id);
    }
}