using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Validations.Common;

internal static class CommonRules
{
    public const int MaxNameLen = 200;
    public const int MaxSlugLen = 200;

    // SKU
    public const int MaxSkuCodeLen = 128;

    // Atributos customizados
    public const int MaxCustomNameLen = 64;
    public const int MaxCustomValueLen = 128;

    // Slug regex padrão
    public const string SlugRegex = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
}