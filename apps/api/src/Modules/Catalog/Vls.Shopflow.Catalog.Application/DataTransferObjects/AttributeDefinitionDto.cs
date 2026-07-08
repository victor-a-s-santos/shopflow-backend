namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record AttributeDefinitionDto(
    Guid Id,
    string Name,
    bool AllowCustomValues,
    Guid? CategoryId,
    IReadOnlyList<AttributeValueDefinitionDto> Values
);