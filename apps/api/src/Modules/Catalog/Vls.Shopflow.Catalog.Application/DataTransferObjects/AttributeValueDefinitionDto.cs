namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record AttributeValueDefinitionDto(
    Guid Id,
    string Name,
    string? HexColor
);