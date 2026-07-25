using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Queries;

public sealed record GetAdminProductsQuery(
    int Page = 1,
    int PageSize = 20,
    string Sort = AdminProductListSort.Default,
    string? Q = null,
    string? CategorySlug = null,
    Guid? CategoryId = null,
    string Status = AdminProductListFilters.StatusAll,
    string Featured = AdminProductListFilters.FeaturedAll) : IQuery<PagedAdminProductsDto>;

public static class AdminProductListFilters
{
    public const string StatusAll = "all";
    public const string StatusActive = "active";
    public const string StatusInactive = "inactive";

    public const string FeaturedAll = "all";
    public const string FeaturedOnly = "featured";
    public const string FeaturedNot = "not_featured";

    public static readonly HashSet<string> StatusAllowed =
        [StatusAll, StatusActive, StatusInactive];

    public static readonly HashSet<string> FeaturedAllowed =
        [FeaturedAll, FeaturedOnly, FeaturedNot];

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return StatusAll;
        var n = status.Trim().ToLowerInvariant();
        return StatusAllowed.Contains(n) ? n : StatusAll;
    }

    public static string NormalizeFeatured(string? featured)
    {
        if (string.IsNullOrWhiteSpace(featured))
            return FeaturedAll;
        var n = featured.Trim().ToLowerInvariant();
        return FeaturedAllowed.Contains(n) ? n : FeaturedAll;
    }
}

public static class AdminProductListSort
{
    public const string Default = "default";
    public const string Newest = "newest";
    public const string Oldest = "oldest";
    public const string NameAsc = "name_asc";
    public const string NameDesc = "name_desc";
    public const string DisplayOrder = "display_order";
    public const string Featured = "featured";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";

    public static readonly HashSet<string> Allowed =
    [
        Default,
        Newest,
        Oldest,
        NameAsc,
        NameDesc,
        DisplayOrder,
        Featured,
        PriceAsc,
        PriceDesc
    ];

    public static string Normalize(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return Default;

        var normalized = sort.Trim().ToLowerInvariant();
        return Allowed.Contains(normalized) ? normalized : Default;
    }
}
