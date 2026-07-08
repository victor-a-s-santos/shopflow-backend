namespace Vls.Shopflow.Catalog.Domain.Entities;

public class AttributeValueDefinition
{
    public Guid Id { get; set; }
    public Guid AttributeDefinitionId { get; set; }
    public string Name { get; set; } = default!;
    public string? HexColor { get; set; }

    public AttributeDefinition AttributeDefinition { get; set; } = default!;
    
    public AttributeValueDefinition(Guid attributeDefinitionId, string name, string? hexColor)
    {
        Id = Guid.NewGuid();
        AttributeDefinitionId = attributeDefinitionId;
        Name = name;
        HexColor = hexColor;
    }
}