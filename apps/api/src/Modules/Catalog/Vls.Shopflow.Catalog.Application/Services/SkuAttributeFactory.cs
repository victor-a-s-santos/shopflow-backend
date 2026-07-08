using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Application.Services;

public static class SkuAttributeFactory
{
    public static IReadOnlyList<SkuAttribute> CreateFromDtos(IReadOnlyList<SkuAttributeCreateDto>? dtos)
    {
        var result = new List<SkuAttribute>();
        var globalDefinitionIds = new HashSet<Guid>();

        foreach (var dto in dtos ?? [])
        {
            if (dto.AttributeDefinitionId is { } definitionId &&
                dto.AttributeValueDefinitionId is { } valueId)
            {
                if (!globalDefinitionIds.Add(definitionId))
                    throw new InvalidOperationException(
                        "A SKU cannot have more than one value for the same global attribute.");

                result.Add(SkuAttribute.FromGlobal(definitionId, valueId));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(dto.CustomName) && !string.IsNullOrWhiteSpace(dto.CustomValue))
            {
                result.Add(SkuAttribute.FromCustom(dto.CustomName.Trim(), dto.CustomValue.Trim()));
            }
        }

        return result;
    }
}
