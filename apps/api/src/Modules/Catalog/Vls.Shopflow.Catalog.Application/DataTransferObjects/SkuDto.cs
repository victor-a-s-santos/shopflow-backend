namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record SkuDto(
    Guid Id,
    string Code,
    decimal RegularPrice,
    decimal? PromotionalPrice,
    decimal EffectivePrice,
    bool IsActive,
    IReadOnlyList<SkuAttributeDto> Attributes,
    SkuSalesRuleDto SalesRule,
    SkuSalesRuleDisplayDto? SalesRuleDisplay = null);
