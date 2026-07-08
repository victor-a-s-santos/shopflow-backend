namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record SkuAttributeDto(
    Guid? AttributeDefinitionId,
    Guid? AttributeValueDefinitionId,
    string? CustomName,
    string? CustomValue,
    string? DefinitionName,
    string? ValueName
);