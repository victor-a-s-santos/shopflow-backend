namespace Vls.Shopflow.Catalog.Domain.Entities;

public class AttributeDefinition
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public bool AllowCustomValues { get; private set; }

    public Guid? CategoryId { get; private set; }
    public Category? Category { get; private set; }

    // 🔒 Encapsula a lista interna
    private readonly List<AttributeValueDefinition> _values = new();
    public IReadOnlyList<AttributeValueDefinition> Values => _values.AsReadOnly();

    private AttributeDefinition(string name, bool allowCustomValues, Guid? categoryId)
    {
        Id = Guid.NewGuid();
        Name = name;
        AllowCustomValues = allowCustomValues;
        CategoryId = categoryId;
    }
    
    public static AttributeDefinition Create(string name, bool allowCustomValues, Guid? categoryId)
        => new(name, allowCustomValues, categoryId);

    /// <summary>
    /// Adiciona um valor global ao atributo (ex: "Preto", "#000000")
    /// com proteção contra duplicações.
    /// </summary>
    public AttributeValueDefinition AddValue(string name, string? hexColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Evita duplicação
        var existing = _values.FirstOrDefault(v =>
            v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            return existing;

        var value = new AttributeValueDefinition(Id, name, hexColor);

        _values.Add(value);
        return value;
    }
}