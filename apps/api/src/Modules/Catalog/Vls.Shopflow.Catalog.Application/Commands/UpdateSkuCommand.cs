using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Commands;

public sealed record UpdateSkuCommand(
    Guid ProductId,
    Guid SkuId,
    string? Code,
    decimal RegularPrice,
    decimal? PromotionalPrice,
    IReadOnlyList<SkuAttributeCreateDto>? Attributes,
    bool Active) : ICommand;
