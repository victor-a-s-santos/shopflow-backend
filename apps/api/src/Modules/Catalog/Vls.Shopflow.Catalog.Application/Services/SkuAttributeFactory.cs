using FluentValidation;
using FluentValidation.Results;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Application.Services;

/// <summary>
/// Resolves and validates SKU attribute DTOs against attribute definitions.
/// Official contract:
/// - predefined: attributeDefinitionId + attributeValueDefinitionId
/// - custom: attributeDefinitionId + customName (value text; AttributeDefinition.AllowCustomValues must be true)
/// </summary>
public static class SkuAttributeFactory
{
    public static async Task<IReadOnlyList<SkuAttribute>> CreateFromDtosAsync(
        IReadOnlyList<SkuAttributeCreateDto>? dtos,
        IAttributeDefinitionLookup lookup,
        string propertyPrefix,
        CancellationToken cancellationToken)
    {
        var list = dtos ?? [];
        if (list.Count == 0)
            return [];

        var definitionIds = list
            .Where(d => d.AttributeDefinitionId.HasValue)
            .Select(d => d.AttributeDefinitionId!.Value)
            .Distinct()
            .ToList();

        var definitions = await lookup.GetByIdsAsync(definitionIds, cancellationToken);
        var failures = new List<ValidationFailure>();
        var result = new List<SkuAttribute>();
        var seenDefinitions = new HashSet<Guid>();

        for (var i = 0; i < list.Count; i++)
        {
            var dto = list[i];
            var path = $"{propertyPrefix}[{i}]";

            if (dto.AttributeDefinitionId is not { } definitionId || definitionId == Guid.Empty)
            {
                failures.Add(new ValidationFailure(
                    $"{path}.attributeDefinitionId",
                    "O atributo deve informar attributeDefinitionId."));
                continue;
            }

            if (!seenDefinitions.Add(definitionId))
            {
                failures.Add(new ValidationFailure(
                    $"{path}.attributeDefinitionId",
                    "A SKU não pode ter mais de um valor para o mesmo atributo."));
                continue;
            }

            if (!definitions.TryGetValue(definitionId, out var definition))
            {
                failures.Add(new ValidationFailure(
                    $"{path}.attributeDefinitionId",
                    "A definição de atributo informada não existe."));
                continue;
            }

            var hasValueId = dto.AttributeValueDefinitionId is { } valueId && valueId != Guid.Empty;
            var hasCustomName = !string.IsNullOrWhiteSpace(dto.CustomName);
            var hasLegacyCustomValue = !string.IsNullOrWhiteSpace(dto.CustomValue);

            if (hasValueId && hasCustomName)
            {
                failures.Add(new ValidationFailure(
                    $"{path}.customName",
                    "Não informe customName junto com attributeValueDefinitionId."));
                failures.Add(new ValidationFailure(
                    $"{path}.attributeValueDefinitionId",
                    "Não informe attributeValueDefinitionId junto com customName."));
                continue;
            }

            if (hasValueId && hasLegacyCustomValue)
            {
                failures.Add(new ValidationFailure(
                    $"{path}.customValue",
                    "Não informe customValue junto com attributeValueDefinitionId."));
                continue;
            }

            if (!hasValueId && !hasCustomName)
            {
                failures.Add(new ValidationFailure(
                    $"{path}.customName",
                    "Informe attributeValueDefinitionId (valor predefinido) ou customName (valor personalizado)."));
                continue;
            }

            if (hasValueId)
            {
                var valueDefinitionId = dto.AttributeValueDefinitionId!.Value;
                if (!definition.Values.TryGetValue(valueDefinitionId, out _))
                {
                    failures.Add(new ValidationFailure(
                        $"{path}.attributeValueDefinitionId",
                        "O valor predefinido não pertence à definição de atributo informada."));
                    continue;
                }

                result.Add(SkuAttribute.FromGlobal(definitionId, valueDefinitionId));
                continue;
            }

            // Custom value on a known definition
            if (!definition.AllowCustomValues)
            {
                failures.Add(new ValidationFailure(
                    $"{path}.customName",
                    $"O atributo “{definition.Name}” não permite valores personalizados."));
                continue;
            }

            var custom = dto.CustomName!.Trim();
            result.Add(SkuAttribute.FromDefinitionCustom(definitionId, custom));
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return result;
    }

    /// <summary>
    /// Labels used for SKU code generation (predefined value names or custom names).
    /// </summary>
    public static async Task<IReadOnlyList<string>> ResolveValueLabelsAsync(
        IReadOnlyList<SkuAttributeCreateDto>? dtos,
        IAttributeDefinitionLookup lookup,
        CancellationToken cancellationToken)
    {
        var list = dtos ?? [];
        if (list.Count == 0)
            return [];

        var definitionIds = list
            .Where(d => d.AttributeDefinitionId.HasValue)
            .Select(d => d.AttributeDefinitionId!.Value)
            .Distinct()
            .ToList();

        var definitions = await lookup.GetByIdsAsync(definitionIds, cancellationToken);
        var labels = new List<string>();

        foreach (var dto in list)
        {
            if (!string.IsNullOrWhiteSpace(dto.CustomName))
            {
                labels.Add(dto.CustomName.Trim());
                continue;
            }

            if (dto.AttributeDefinitionId is { } defId &&
                dto.AttributeValueDefinitionId is { } valId &&
                definitions.TryGetValue(defId, out var def) &&
                def.Values.TryGetValue(valId, out var value))
            {
                labels.Add(value.Name);
            }
        }

        return labels;
    }
}
