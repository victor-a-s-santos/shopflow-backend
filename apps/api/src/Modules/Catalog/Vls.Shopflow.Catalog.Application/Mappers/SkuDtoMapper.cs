using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Application.Mappers;

public static class SkuDtoMapper
{
    public static SkuDto FromEntity(Sku sku)
    {
        var regular = sku.Price.Regular.Amount;
        var promo = sku.Price.Promotional?.Amount;
        var effective = promo.HasValue
            ? Math.Min(regular, promo.Value)
            : regular;

        return new SkuDto(
            sku.Id,
            sku.Code,
            regular,
            promo,
            effective,
            sku.IsActive,
            sku.Attributes.Select(a => new SkuAttributeDto(
                a.AttributeDefinitionId,
                a.AttributeValueDefinitionId,
                a.CustomName,
                a.CustomValue,
                a.AttributeDefinition?.Name,
                a.AttributeValueDefinition?.Name
            )).ToList(),
            SkuSalesRuleFactory.ToDto(sku.SalesRule),
            SkuSalesRuleFactory.ToDisplayDto(sku.SalesRule, regular, promo));
    }
}
