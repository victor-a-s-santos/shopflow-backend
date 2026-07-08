namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record SkuAttributeCreateDto(
    Guid? AttributeDefinitionId,
    Guid? AttributeValueDefinitionId,
    string? CustomName,
    string? CustomValue
);