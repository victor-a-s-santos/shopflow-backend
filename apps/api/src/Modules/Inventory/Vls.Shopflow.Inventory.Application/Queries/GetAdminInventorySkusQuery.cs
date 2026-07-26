using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;

namespace Vls.Shopflow.Inventory.Application.Queries;

public sealed record GetAdminInventorySkusQuery(
    int Page = 1,
    int PageSize = 20,
    string Sort = AdminInventorySkuListSort.Default,
    string? Q = null,
    Guid? ProductId = null,
    string? CategorySlug = null,
    Guid? CategoryId = null,
    string Status = AdminInventorySkuListFilters.StatusAll,
    string StockStatus = AdminInventorySkuListFilters.StockAll) : IQuery<PagedAdminInventorySkusDto>;

public static class AdminInventorySkuListFilters
{
    public const string StatusAll = "all";
    public const string StatusActive = "active";
    public const string StatusInactive = "inactive";

    public const string StockAll = "all";
    public const string StockInStock = "in_stock";
    public const string StockLowStock = "low_stock";
    public const string StockOutOfStock = "out_of_stock";
    public const string StockReserved = "reserved";

    /// <summary>Default low-stock threshold when no config exists.</summary>
    public const int LowStockThreshold = 5;

    public static readonly HashSet<string> StatusAllowed =
        [StatusAll, StatusActive, StatusInactive];

    public static readonly HashSet<string> StockAllowed =
        [StockAll, StockInStock, StockLowStock, StockOutOfStock, StockReserved];

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return StatusAll;
        var n = status.Trim().ToLowerInvariant();
        return StatusAllowed.Contains(n) ? n : StatusAll;
    }

    public static string NormalizeStockStatus(string? stockStatus)
    {
        if (string.IsNullOrWhiteSpace(stockStatus))
            return StockAll;
        var n = stockStatus.Trim().ToLowerInvariant();
        return StockAllowed.Contains(n) ? n : StockAll;
    }
}

public static class AdminInventorySkuListSort
{
    public const string Default = "default";
    public const string ProductNameAsc = "product_name_asc";
    public const string ProductNameDesc = "product_name_desc";
    public const string SkuCodeAsc = "sku_code_asc";
    public const string SkuCodeDesc = "sku_code_desc";
    public const string StockAsc = "stock_asc";
    public const string StockDesc = "stock_desc";
    public const string AvailableAsc = "available_asc";
    public const string AvailableDesc = "available_desc";
    public const string ReservedDesc = "reserved_desc";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";

    public static readonly HashSet<string> Allowed =
    [
        Default,
        ProductNameAsc,
        ProductNameDesc,
        SkuCodeAsc,
        SkuCodeDesc,
        StockAsc,
        StockDesc,
        AvailableAsc,
        AvailableDesc,
        ReservedDesc,
        PriceAsc,
        PriceDesc
    ];

    public static string Normalize(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return Default;
        var n = sort.Trim().ToLowerInvariant();
        return Allowed.Contains(n) ? n : Default;
    }
}
