using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Infrastructure.Repositories;

/// <summary>
/// Read-only admin reports.
/// Confirmed demand is <see cref="OrderStatus.Paid"/> only. Quantity is the order-item
/// quantity (SKU commercial units), never multiplied by package size.
/// Unpaid follow-up is <see cref="OrderStatus.PendingPayment"/> only.
/// </summary>
public sealed class AdminDemandReportReadModel(OrdersDbContext db) : IAdminDemandReportReadModel
{
    public async Task<AdminDemandSummaryPage> GetProductDemandAsync(
        AdminDemandSummaryQuerySpec spec,
        CancellationToken cancellationToken)
    {
        var items = FilteredPaidItems(spec.From, spec.To, spec.SearchText, spec.SearchSkuId);

        var perOrderSku = items
            .GroupBy(item => new { item.SkuId, item.OrderId })
            .Select(group => new
            {
                group.Key.SkuId,
                Quantity = group.Sum(item => item.Quantity),
                Subtotal = group.Sum(item => item.Subtotal)
            });

        var grouped = perOrderSku
            .GroupBy(row => row.SkuId)
            .Select(group => new
            {
                SkuId = group.Key,
                TotalQuantity = group.Sum(row => row.Quantity),
                OrderCount = group.Count(),
                SubtotalTotal = group.Sum(row => row.Subtotal)
            });

        var quantityAsc = Is(spec.Sort, AdminDemandReportSort.QuantityAsc);
        var ordersDesc = Is(spec.Sort, AdminDemandReportSort.OrdersDesc);
        var ordersAsc = Is(spec.Sort, AdminDemandReportSort.OrdersAsc);
        var subtotalDesc = Is(spec.Sort, AdminDemandReportSort.SubtotalDesc);
        var subtotalAsc = Is(spec.Sort, AdminDemandReportSort.SubtotalAsc);

        var ordered = quantityAsc
            ? grouped.OrderBy(row => row.TotalQuantity).ThenBy(row => row.SkuId)
            : ordersDesc
                ? grouped.OrderByDescending(row => row.OrderCount).ThenBy(row => row.SkuId)
                : ordersAsc
                    ? grouped.OrderBy(row => row.OrderCount).ThenBy(row => row.SkuId)
                    : subtotalDesc
                        ? grouped.OrderByDescending(row => row.SubtotalTotal).ThenBy(row => row.SkuId)
                        : subtotalAsc
                            ? grouped.OrderBy(row => row.SubtotalTotal).ThenBy(row => row.SkuId)
                            : grouped.OrderByDescending(row => row.TotalQuantity).ThenBy(row => row.SkuId);

        var totalItems = await ordered.CountAsync(cancellationToken);
        var page = await ordered
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .ToListAsync(cancellationToken);

        var snapshots = await LoadLatestSnapshotsAsync(
            page.Select(row => row.SkuId).ToArray(),
            spec.From,
            spec.To,
            spec.SearchText,
            spec.SearchSkuId,
            cancellationToken);

        var rows = page.Select(row =>
        {
            snapshots.TryGetValue(row.SkuId, out var snapshot);
            return new AdminDemandSummaryRow(
                row.SkuId,
                snapshot?.ProductName ?? string.Empty,
                snapshot?.SkuCode ?? string.Empty,
                row.TotalQuantity,
                row.OrderCount,
                row.SubtotalTotal,
                snapshot?.SalesMode,
                snapshot?.PackageSize,
                snapshot?.PackageLabel,
                snapshot?.PackageDescription,
                snapshot?.QuantityUnitLabel,
                snapshot?.ShowTotalPieces,
                snapshot?.TotalPieces,
                snapshot?.EquivalentUnitPrice,
                snapshot?.SalesDisplaySummary);
        }).ToList();

        return new AdminDemandSummaryPage(rows, totalItems);
    }

    public async Task<AdminDemandOrdersPage> GetProductDemandOrdersAsync(
        AdminDemandOrdersQuerySpec spec,
        CancellationToken cancellationToken)
    {
        var items = FilteredPaidItems(spec.From, spec.To, searchText: null, spec.SkuId);

        var perOrder = items
            .GroupBy(item => item.OrderId)
            .Select(group => new
            {
                OrderId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                Subtotal = group.Sum(item => item.Subtotal),
                MinUnitPrice = group.Min(item => item.UnitPrice),
                MaxUnitPrice = group.Max(item => item.UnitPrice),
                LineCount = group.Count()
            });

        var joined =
            from aggregate in perOrder
            join order in db.Orders.AsNoTracking() on aggregate.OrderId equals order.Id
            select new { aggregate, order };

        var paidAtAsc = Is(spec.Sort, AdminDemandReportSort.PaidAtAsc);
        var createdAtDesc = Is(spec.Sort, AdminDemandReportSort.CreatedAtDesc);
        var createdAtAsc = Is(spec.Sort, AdminDemandReportSort.CreatedAtAsc);

        var ordered = paidAtAsc
            ? joined.OrderBy(row => row.order.PaidAt ?? row.order.CreatedAt).ThenBy(row => row.order.Id)
            : createdAtDesc
                ? joined.OrderByDescending(row => row.order.CreatedAt).ThenByDescending(row => row.order.Id)
                : createdAtAsc
                    ? joined.OrderBy(row => row.order.CreatedAt).ThenBy(row => row.order.Id)
                    : joined.OrderByDescending(row => row.order.PaidAt ?? row.order.CreatedAt)
                        .ThenByDescending(row => row.order.Id);

        var totalItems = await ordered.CountAsync(cancellationToken);
        var page = await ordered
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .Select(row => new DemandOrderHeader
            {
                OrderId = row.order.Id,
                OrderNumber = row.order.OrderNumber,
                CreatedAt = row.order.CreatedAt,
                PaidAt = row.order.PaidAt,
                Status = row.order.Status,
                CustomerUserId = row.order.CustomerUserId,
                CustomerFullName = row.order.CustomerFullName,
                CustomerEmail = row.order.CustomerEmail,
                CustomerPhone = row.order.CustomerPhone,
                Quantity = row.aggregate.Quantity,
                Subtotal = row.aggregate.Subtotal,
                MinUnitPrice = row.aggregate.MinUnitPrice,
                MaxUnitPrice = row.aggregate.MaxUnitPrice,
                LineCount = row.aggregate.LineCount
            })
            .ToListAsync(cancellationToken);

        var orderIds = page.Select(row => row.OrderId).ToArray();
        var displays = orderIds.Length == 0
            ? new Dictionary<Guid, ItemDisplayRow>()
            : await LoadOrderDisplaysAsync(spec.SkuId, orderIds, cancellationToken);

        var rows = page.Select(row =>
        {
            displays.TryGetValue(row.OrderId, out var display);
            var samePrice = row.MinUnitPrice == row.MaxUnitPrice;
            var singleLine = row.LineCount == 1;
            return new AdminDemandOrderRow(
                row.OrderId,
                row.OrderNumber,
                row.CreatedAt,
                row.PaidAt,
                row.Status,
                row.CustomerUserId,
                row.CustomerFullName,
                row.CustomerEmail,
                row.CustomerPhone,
                row.Quantity,
                samePrice ? row.MinUnitPrice : null,
                row.Subtotal,
                display?.ProductName ?? string.Empty,
                display?.SkuCode ?? string.Empty,
                singleLine ? display?.SalesMode : null,
                singleLine ? display?.PackageSize : null,
                singleLine ? display?.PackageLabel : null,
                singleLine ? display?.PackageDescription : null,
                singleLine ? display?.QuantityUnitLabel : null,
                singleLine ? display?.ShowTotalPieces : null,
                singleLine ? display?.TotalPieces : null,
                singleLine ? display?.EquivalentUnitPrice : null,
                singleLine ? display?.SalesDisplaySummary : null);
        }).ToList();

        return new AdminDemandOrdersPage(rows, totalItems);
    }

    public async Task<AdminUnpaidOrdersPage> GetUnpaidOrdersAsync(
        AdminUnpaidOrdersQuerySpec spec,
        CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking()
            .Where(order => order.Status == OrderStatus.PendingPayment);

        if (spec.From is { } from)
            query = query.Where(order => order.CreatedAt >= from);

        if (spec.To is { } to)
            query = query.Where(order => order.CreatedAt <= to);

        if (spec.ExcludeOrderIds is { Count: > 0 } excluded)
            query = query.Where(order => !excluded.Contains(order.Id));

        if (spec.SearchOrderOrSkuId is { } id)
            query = query.Where(order => order.Id == id || order.Items.Any(item => item.SkuId == id));
        else if (spec.SearchOrderNumber is { } orderNumber)
            query = query.Where(order => order.OrderNumber == orderNumber);
        else if (!string.IsNullOrWhiteSpace(spec.SearchText))
        {
            var term = spec.SearchText.Trim().ToLower();
            query = query.Where(order =>
                order.CustomerEmail.ToLower().Contains(term)
                || order.CustomerFullName.ToLower().Contains(term)
                || order.CustomerPhone.ToLower().Contains(term)
                || order.OrderNumber.ToString().Contains(term)
                || order.Items.Any(item =>
                    item.ProductName.ToLower().Contains(term)
                    || item.SkuCode.ToLower().Contains(term)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var ordered = spec.SortCreatedAtAscending
            ? query.OrderBy(order => order.CreatedAt).ThenBy(order => order.Id)
            : query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id);

        var headers = await ordered
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .Select(order => new UnpaidHeader
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CreatedAt = order.CreatedAt,
                Status = order.Status,
                CustomerUserId = order.CustomerUserId,
                CustomerFullName = order.CustomerFullName,
                CustomerEmail = order.CustomerEmail,
                CustomerPhone = order.CustomerPhone,
                Subtotal = order.Subtotal,
                ShippingAmount = order.ShippingAmount,
                Total = order.Total
            })
            .ToListAsync(cancellationToken);

        var orderIds = headers.Select(row => row.Id).ToArray();
        var items = orderIds.Length == 0
            ? []
            : await db.OrderItems.AsNoTracking()
                .Where(item => orderIds.Contains(item.OrderId))
                .OrderBy(item => item.Id)
                .Select(item => new UnpaidItemHeader
                {
                    OrderId = item.OrderId,
                    Id = item.Id,
                    SkuId = item.SkuId,
                    SkuCode = item.SkuCode,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal,
                    SalesMode = item.SalesMode,
                    PackageSize = item.PackageSize,
                    PackageLabel = item.PackageLabel,
                    PackageDescription = item.PackageDescription,
                    QuantityUnitLabel = item.QuantityUnitLabel,
                    ShowTotalPieces = item.ShowTotalPieces,
                    TotalPieces = item.TotalPieces,
                    EquivalentUnitPrice = item.EquivalentUnitPrice,
                    SalesDisplaySummary = item.SalesDisplaySummary
                })
                .ToListAsync(cancellationToken);

        var itemsByOrder = items
            .GroupBy(item => item.OrderId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AdminUnpaidOrderItemRow>)group.Select(item => new AdminUnpaidOrderItemRow(
                    item.Id,
                    item.SkuId,
                    item.SkuCode,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice,
                    item.Subtotal,
                    item.SalesMode,
                    item.PackageSize,
                    item.PackageLabel,
                    item.PackageDescription,
                    item.QuantityUnitLabel,
                    item.ShowTotalPieces,
                    item.TotalPieces,
                    item.EquivalentUnitPrice,
                    item.SalesDisplaySummary)).ToList());

        var rows = headers.Select(row => new AdminUnpaidOrderRow(
            row.Id,
            row.OrderNumber,
            row.CreatedAt,
            row.Status,
            row.CustomerUserId,
            row.CustomerFullName,
            row.CustomerEmail,
            row.CustomerPhone,
            row.Subtotal,
            row.ShippingAmount,
            row.Total,
            itemsByOrder.TryGetValue(row.Id, out var orderItems) ? orderItems : [])).ToList();

        return new AdminUnpaidOrdersPage(rows, totalItems);
    }

    private IQueryable<Domain.Entities.OrderItem> FilteredPaidItems(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? searchText,
        Guid? searchSkuId)
    {
        var paidOrderIds = PaidOrders(from, to).Select(order => order.Id);
        var items = db.OrderItems.AsNoTracking().Where(item => paidOrderIds.Contains(item.OrderId));

        if (searchSkuId is { } skuId)
            items = items.Where(item => item.SkuId == skuId);
        else if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLower();
            items = items.Where(item =>
                item.ProductName.ToLower().Contains(term)
                || item.SkuCode.ToLower().Contains(term));
        }

        return items;
    }

    private IQueryable<Domain.Entities.Order> PaidOrders(DateTimeOffset? from, DateTimeOffset? to)
    {
        var query = db.Orders.AsNoTracking().Where(order => order.Status == OrderStatus.Paid);

        if (from is { } fromValue)
            query = query.Where(order => (order.PaidAt ?? order.CreatedAt) >= fromValue);

        if (to is { } toValue)
            query = query.Where(order => (order.PaidAt ?? order.CreatedAt) <= toValue);

        return query;
    }

    private async Task<Dictionary<Guid, SnapshotRow>> LoadLatestSnapshotsAsync(
        Guid[] skuIds,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? searchText,
        Guid? searchSkuId,
        CancellationToken cancellationToken)
    {
        if (skuIds.Length == 0)
            return [];

        var parameters = new List<object>(skuIds.Length + 3);
        var placeholders = new string[skuIds.Length];
        for (var i = 0; i < skuIds.Length; i++)
        {
            var name = "sku" + i;
            placeholders[i] = "@" + name;
            parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = skuIds[i] });
        }

        parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz)
        {
            Value = from.HasValue ? from.Value : DBNull.Value
        });
        parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz)
        {
            Value = to.HasValue ? to.Value : DBNull.Value
        });

        string? pattern = null;
        if (searchSkuId is null && !string.IsNullOrWhiteSpace(searchText))
            pattern = LikeContainsPattern(searchText);

        parameters.Add(new NpgsqlParameter("pattern", NpgsqlDbType.Text)
        {
            Value = pattern is null ? DBNull.Value : pattern
        });

        var sql = $"""
            SELECT DISTINCT ON (i."SkuId")
                i."SkuId" AS "SkuId",
                i."ProductName" AS "ProductName",
                i."SkuCode" AS "SkuCode",
                i."SalesMode" AS "SalesMode",
                i."PackageSize" AS "PackageSize",
                i."PackageLabel" AS "PackageLabel",
                i."PackageDescription" AS "PackageDescription",
                i."QuantityUnitLabel" AS "QuantityUnitLabel",
                i."ShowTotalPieces" AS "ShowTotalPieces",
                i."TotalPieces" AS "TotalPieces",
                i."EquivalentUnitPrice" AS "EquivalentUnitPrice",
                i."SalesDisplaySummary" AS "SalesDisplaySummary"
            FROM orders.order_items i
            INNER JOIN orders.orders o ON o."Id" = i."OrderId"
            WHERE o."Status" = 'Paid'
              AND i."SkuId" IN ({string.Join(", ", placeholders)})
              AND (@from IS NULL OR COALESCE(o."PaidAt", o."CreatedAt") >= @from)
              AND (@to IS NULL OR COALESCE(o."PaidAt", o."CreatedAt") <= @to)
              AND (
                    @pattern IS NULL
                    OR LOWER(i."ProductName") LIKE @pattern ESCAPE '\'
                    OR LOWER(i."SkuCode") LIKE @pattern ESCAPE '\'
                  )
            ORDER BY i."SkuId", COALESCE(o."PaidAt", o."CreatedAt") DESC, o."Id" DESC, i."Id" DESC
            """;

        var rows = await db.Database.SqlQueryRaw<SnapshotRow>(sql, parameters.ToArray())
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.SkuId);
    }

    private async Task<Dictionary<Guid, ItemDisplayRow>> LoadOrderDisplaysAsync(
        Guid skuId,
        Guid[] orderIds,
        CancellationToken cancellationToken)
    {
        var rows = await db.OrderItems.AsNoTracking()
            .Where(item => item.SkuId == skuId && orderIds.Contains(item.OrderId))
            .Select(item => new ItemDisplayRow
            {
                OrderId = item.OrderId,
                ItemId = item.Id,
                ProductName = item.ProductName,
                SkuCode = item.SkuCode,
                SalesMode = item.SalesMode,
                PackageSize = item.PackageSize,
                PackageLabel = item.PackageLabel,
                PackageDescription = item.PackageDescription,
                QuantityUnitLabel = item.QuantityUnitLabel,
                ShowTotalPieces = item.ShowTotalPieces,
                TotalPieces = item.TotalPieces,
                EquivalentUnitPrice = item.EquivalentUnitPrice,
                SalesDisplaySummary = item.SalesDisplaySummary
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.OrderId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(row => row.ItemId).First());
    }

    private static bool Is(string sort, string expected)
        => string.Equals(sort, expected, StringComparison.OrdinalIgnoreCase);

    private static string LikeContainsPattern(string searchText)
    {
        var escaped = searchText.Trim().ToLower()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return "%" + escaped + "%";
    }

    private sealed class SnapshotRow
    {
        public Guid SkuId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string? SalesMode { get; set; }
        public int? PackageSize { get; set; }
        public string? PackageLabel { get; set; }
        public string? PackageDescription { get; set; }
        public string? QuantityUnitLabel { get; set; }
        public bool? ShowTotalPieces { get; set; }
        public int? TotalPieces { get; set; }
        public decimal? EquivalentUnitPrice { get; set; }
        public string? SalesDisplaySummary { get; set; }
    }

    private sealed class ItemDisplayRow
    {
        public Guid OrderId { get; set; }
        public Guid ItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string? SalesMode { get; set; }
        public int? PackageSize { get; set; }
        public string? PackageLabel { get; set; }
        public string? PackageDescription { get; set; }
        public string? QuantityUnitLabel { get; set; }
        public bool? ShowTotalPieces { get; set; }
        public int? TotalPieces { get; set; }
        public decimal? EquivalentUnitPrice { get; set; }
        public string? SalesDisplaySummary { get; set; }
    }

    private sealed class DemandOrderHeader
    {
        public Guid OrderId { get; set; }
        public long OrderNumber { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? PaidAt { get; set; }
        public OrderStatus Status { get; set; }
        public Guid? CustomerUserId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
        public decimal MinUnitPrice { get; set; }
        public decimal MaxUnitPrice { get; set; }
        public int LineCount { get; set; }
    }

    private sealed class UnpaidHeader
    {
        public Guid Id { get; set; }
        public long OrderNumber { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public OrderStatus Status { get; set; }
        public Guid? CustomerUserId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal? ShippingAmount { get; set; }
        public decimal Total { get; set; }
    }

    private sealed class UnpaidItemHeader
    {
        public Guid OrderId { get; set; }
        public Guid Id { get; set; }
        public Guid SkuId { get; set; }
        public string SkuCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string? SalesMode { get; set; }
        public int? PackageSize { get; set; }
        public string? PackageLabel { get; set; }
        public string? PackageDescription { get; set; }
        public string? QuantityUnitLabel { get; set; }
        public bool? ShowTotalPieces { get; set; }
        public int? TotalPieces { get; set; }
        public decimal? EquivalentUnitPrice { get; set; }
        public string? SalesDisplaySummary { get; set; }
    }
}
