using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Queries;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 16,
    string Sort = ProductListSort.Default,
    string? CategorySlug = null,
    Guid? CategoryId = null,
    string? Q = null) : IQuery<PagedProductsDto>;

public static class ProductListSort
{
    public const string Default = "default";
    public const string Newest = "newest";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";
    public const string NameAsc = "name_asc";

    public static readonly HashSet<string> Allowed =
    [
        Default,
        Newest,
        PriceAsc,
        PriceDesc,
        NameAsc
    ];

    public static string Normalize(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return Default;

        var normalized = sort.Trim().ToLowerInvariant();
        return Allowed.Contains(normalized) ? normalized : Default;
    }
}
