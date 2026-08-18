using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Queries;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Application.Services;

namespace Vls.Shopflow.Inventory.Infrastructure.Repositories;

/// <summary>
/// Cross-schema admin SKU listing: catalog.product_skus + products (+ category/image)
/// LEFT JOIN inventory.inventory_items. Single count + single page query (no N+1).
/// </summary>
public sealed class AdminInventorySkuReadModel(InventoryDbContext db) : IAdminInventorySkuReadModel
{
    private const int LowStockThreshold = AdminInventorySkuListFilters.LowStockThreshold;

    public async Task<PagedAdminInventorySkusDto> GetPagedAsync(
        int page,
        int pageSize,
        string sort,
        string? q,
        Guid? productId,
        string? categorySlug,
        Guid? categoryId,
        string status,
        string stockStatus,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        sort = AdminInventorySkuListSort.Normalize(sort);
        status = AdminInventorySkuListFilters.NormalizeStatus(status);
        stockStatus = AdminInventorySkuListFilters.NormalizeStockStatus(stockStatus);

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(ct);

        var where = new StringBuilder("TRUE");
        var parameters = new List<(string Name, object? Value)>();

        void AddParam(string name, object? value) => parameters.Add((name, value));

        if (productId is { } pid && pid != Guid.Empty)
        {
            where.Append(" AND s.\"ProductId\" = @productId");
            AddParam("@productId", pid);
        }

        if (categoryId is { } cid && cid != Guid.Empty)
        {
            where.Append(" AND p.\"CategoryId\" = @categoryId");
            AddParam("@categoryId", cid);
        }

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            where.Append(" AND c.slug = @categorySlug");
            AddParam("@categorySlug", categorySlug.Trim().ToLowerInvariant());
        }

        if (status == AdminInventorySkuListFilters.StatusActive)
            where.Append(" AND s.\"IsActive\" = TRUE AND p.\"IsActive\" = TRUE");
        else if (status == AdminInventorySkuListFilters.StatusInactive)
            where.Append(" AND (s.\"IsActive\" = FALSE OR p.\"IsActive\" = FALSE)");

        if (!string.IsNullOrWhiteSpace(q))
        {
            where.Append(
                """
                 AND (
                    LOWER(p."Name") LIKE @q
                    OR LOWER(p.slug) LIKE @q
                    OR LOWER(s."Code") LIKE @q
                )
                """);
            AddParam("@q", $"%{q.Trim().ToLowerInvariant()}%");
        }

        // Stock quantities (NULL inventory row => 0)
        const string availableExpr =
            "GREATEST(COALESCE(inv.\"QuantityOnHand\", 0) - COALESCE(inv.\"QuantityReserved\", 0), 0)";
        const string physicalExpr = "COALESCE(inv.\"QuantityOnHand\", 0)";
        const string reservedExpr = "COALESCE(inv.\"QuantityReserved\", 0)";

        where.Append(stockStatus switch
        {
            AdminInventorySkuListFilters.StockInStock =>
                $" AND {availableExpr} > @lowStockThreshold",
            AdminInventorySkuListFilters.StockLowStock =>
                $" AND {availableExpr} > 0 AND {availableExpr} <= @lowStockThreshold",
            AdminInventorySkuListFilters.StockOutOfStock =>
                $" AND {availableExpr} <= 0",
            AdminInventorySkuListFilters.StockReserved =>
                $" AND {reservedExpr} > 0",
            _ => string.Empty
        });

        if (stockStatus is AdminInventorySkuListFilters.StockInStock
            or AdminInventorySkuListFilters.StockLowStock)
        {
            AddParam("@lowStockThreshold", LowStockThreshold);
        }

        const string effectivePriceExpr = """
            CASE
                WHEN s.promo_price IS NOT NULL AND s.promo_price < s.regular_price THEN s.promo_price
                ELSE s.regular_price
            END
            """;

        var fromSql = $"""
            FROM catalog.product_skus s
            INNER JOIN catalog.products p ON p."Id" = s."ProductId"
            LEFT JOIN catalog.categories c ON c."Id" = p."CategoryId"
            LEFT JOIN inventory.inventory_items inv ON inv."SkuId" = s."Id"
            WHERE {where}
            """;

        var totalItems = await ExecuteScalarIntAsync(
            connection,
            $"SELECT COUNT(*)::int {fromSql}",
            parameters,
            ct);

        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var orderBy = BuildOrderBy(sort, physicalExpr, availableExpr, reservedExpr, effectivePriceExpr);

        var offset = (page - 1) * pageSize;
        AddParam("@limit", pageSize);
        AddParam("@offset", offset);

        var selectSql = $"""
            SELECT
                s."Id" AS "SkuId",
                p."Id" AS "ProductId",
                p."Name" AS "ProductName",
                p.slug AS "ProductSlug",
                p."IsActive" AS "ProductIsActive",
                s."Code" AS "SkuCode",
                s."IsActive" AS "SkuIsActive",
                c."Id" AS "CategoryId",
                c."Name" AS "CategoryName",
                c.slug AS "CategorySlug",
                (
                    SELECT img."Url"
                    FROM catalog.product_images img
                    WHERE img."ProductId" = p."Id"
                    ORDER BY img."IsPrimary" DESC, img."SortOrder" ASC
                    LIMIT 1
                ) AS "PrimaryImageUrl",
                s.regular_price AS "RegularPrice",
                s.promo_price AS "PromotionalPrice",
                {effectivePriceExpr} AS "EffectivePrice",
                {physicalExpr} AS "PhysicalQuantity",
                {reservedExpr} AS "ReservedQuantity",
                {availableExpr} AS "AvailableQuantity",
                COALESCE(s.sales_mode, 0) AS "SalesMode",
                s.package_size AS "PackageSize",
                s.package_label AS "PackageLabel",
                s.quantity_unit_label AS "QuantityUnitLabel",
                p."CreatedAt" AS "CreatedAt"
            {fromSql}
            ORDER BY {orderBy}
            LIMIT @limit OFFSET @offset
            """;

        var items = await ExecuteItemsAsync(connection, selectSql, parameters, ct);

        return new PagedAdminInventorySkusDto(
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            page < totalPages,
            page > 1 && totalPages > 0);
    }

    private static string BuildOrderBy(
        string sort,
        string physicalExpr,
        string availableExpr,
        string reservedExpr,
        string effectivePriceExpr)
        => sort switch
        {
            AdminInventorySkuListSort.ProductNameDesc =>
                """p."Name" DESC, s."Code" ASC, s."Id" ASC""",
            AdminInventorySkuListSort.SkuCodeAsc =>
                """s."Code" ASC, p."Name" ASC, s."Id" ASC""",
            AdminInventorySkuListSort.SkuCodeDesc =>
                """s."Code" DESC, p."Name" ASC, s."Id" ASC""",
            AdminInventorySkuListSort.StockAsc =>
                $"{physicalExpr} ASC, p.\"Name\" ASC, s.\"Id\" ASC",
            AdminInventorySkuListSort.StockDesc =>
                $"{physicalExpr} DESC, p.\"Name\" ASC, s.\"Id\" ASC",
            AdminInventorySkuListSort.AvailableAsc =>
                $"{availableExpr} ASC, p.\"Name\" ASC, s.\"Id\" ASC",
            AdminInventorySkuListSort.AvailableDesc =>
                $"{availableExpr} DESC, p.\"Name\" ASC, s.\"Id\" ASC",
            AdminInventorySkuListSort.ReservedDesc =>
                $"{reservedExpr} DESC, p.\"Name\" ASC, s.\"Id\" ASC",
            AdminInventorySkuListSort.PriceAsc =>
                $"{effectivePriceExpr} ASC NULLS LAST, p.\"Name\" ASC, s.\"Id\" ASC",
            AdminInventorySkuListSort.PriceDesc =>
                $"{effectivePriceExpr} DESC NULLS LAST, p.\"Name\" ASC, s.\"Id\" ASC",
            // default + product_name_asc
            _ => """p."Name" ASC, s."Code" ASC, s."Id" ASC"""
        };

    private static async Task<int> ExecuteScalarIntAsync(
        DbConnection connection,
        string sql,
        IReadOnlyList<(string Name, object? Value)> parameters,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AttachParameters(cmd, parameters);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int i ? i : Convert.ToInt32(result);
    }

    private static async Task<IReadOnlyList<AdminInventorySkuListItemDto>> ExecuteItemsAsync(
        DbConnection connection,
        string sql,
        IReadOnlyList<(string Name, object? Value)> parameters,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AttachParameters(cmd, parameters);

        var items = new List<AdminInventorySkuListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var physical = reader.GetInt32(reader.GetOrdinal("PhysicalQuantity"));
            var reserved = reader.GetInt32(reader.GetOrdinal("ReservedQuantity"));
            var available = reader.GetInt32(reader.GetOrdinal("AvailableQuantity"));
            var salesModeInt = reader.GetInt32(reader.GetOrdinal("SalesMode"));

            Guid? categoryId = reader.IsDBNull(reader.GetOrdinal("CategoryId"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("CategoryId"));
            string? categoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName"))
                ? null
                : reader.GetString(reader.GetOrdinal("CategoryName"));
            string? categorySlug = reader.IsDBNull(reader.GetOrdinal("CategorySlug"))
                ? null
                : reader.GetString(reader.GetOrdinal("CategorySlug"));

            items.Add(new AdminInventorySkuListItemDto(
                reader.GetGuid(reader.GetOrdinal("SkuId")),
                reader.GetGuid(reader.GetOrdinal("ProductId")),
                reader.GetString(reader.GetOrdinal("ProductName")),
                reader.GetString(reader.GetOrdinal("ProductSlug")),
                reader.GetBoolean(reader.GetOrdinal("ProductIsActive")),
                reader.GetString(reader.GetOrdinal("SkuCode")),
                reader.GetBoolean(reader.GetOrdinal("SkuIsActive")),
                categoryId is { } cid && categoryName is not null && categorySlug is not null
                    ? new AdminInventorySkuCategoryDto(cid, categoryName, categorySlug)
                    : null,
                reader.IsDBNull(reader.GetOrdinal("PrimaryImageUrl"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("PrimaryImageUrl")),
                reader.GetDecimal(reader.GetOrdinal("RegularPrice")),
                reader.IsDBNull(reader.GetOrdinal("PromotionalPrice"))
                    ? null
                    : reader.GetDecimal(reader.GetOrdinal("PromotionalPrice")),
                reader.GetDecimal(reader.GetOrdinal("EffectivePrice")),
                physical,
                reserved,
                available,
                AdminInventoryStockStatus.Compute(available, reserved),
                MapSalesMode(salesModeInt),
                reader.IsDBNull(reader.GetOrdinal("PackageSize"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("PackageSize")),
                reader.IsDBNull(reader.GetOrdinal("PackageLabel"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("PackageLabel")),
                reader.IsDBNull(reader.GetOrdinal("QuantityUnitLabel"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("QuantityUnitLabel")),
                reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt"))));
        }

        return items;
    }

    private static void AttachParameters(
        DbCommand cmd,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }

    private static string MapSalesMode(int salesMode) => salesMode switch
    {
        1 => "MinimumQuantity",
        2 => "MultipleQuantity",
        3 => "FixedPackage",
        4 => "AssortedPackage",
        _ => "Unit"
    };
}
