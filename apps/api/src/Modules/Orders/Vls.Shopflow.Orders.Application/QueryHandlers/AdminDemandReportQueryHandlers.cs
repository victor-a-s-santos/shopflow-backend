using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Services;

namespace Vls.Shopflow.Orders.Application.QueryHandlers;

public sealed class GetAdminProductDemandQueryHandler(IAdminDemandReportReadModel readModel)
    : IQueryHandler<GetAdminProductDemandQuery, PagedAdminProductDemandDto>
{
    public async Task<PagedAdminProductDemandDto> Handle(
        GetAdminProductDemandQuery query,
        CancellationToken cancellationToken)
    {
        Guid? searchSkuId = null;
        string? searchText = null;
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var raw = query.Q.Trim();
            if (Guid.TryParse(raw, out var skuId) && skuId != Guid.Empty)
                searchSkuId = skuId;
            else
                searchText = raw;
        }

        var page = await readModel.GetProductDemandAsync(
            new AdminDemandSummaryQuerySpec(
                query.Page,
                query.PageSize,
                query.From,
                query.To,
                searchText,
                searchSkuId,
                AdminDemandReportSort.Normalize(query.Sort, AdminDemandReportSort.QuantityDesc)),
            cancellationToken);

        var items = page.Items.Select(row => new AdminProductDemandItemDto(
            row.SkuId,
            row.ProductName,
            row.SkuCode,
            row.TotalQuantity,
            row.OrderCount,
            row.SubtotalTotal,
            Sales(row.SalesMode, row.PackageSize, row.PackageLabel, row.PackageDescription, row.QuantityUnitLabel, row.ShowTotalPieces, row.TotalPieces, row.EquivalentUnitPrice, row.SalesDisplaySummary)))
            .ToList();

        return new PagedAdminProductDemandDto(
            items,
            query.Page,
            query.PageSize,
            page.TotalItems,
            TotalPages(page.TotalItems, query.PageSize));
    }

    internal static int TotalPages(int totalItems, int pageSize)
        => totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

    internal static OrderItemSalesDisplayDto? Sales(
        string? salesMode,
        int? packageSize,
        string? packageLabel,
        string? packageDescription,
        string? quantityUnitLabel,
        bool? showTotalPieces,
        int? totalPieces,
        decimal? equivalentUnitPrice,
        string? salesDisplaySummary)
        => OrderItemSalesDisplayMapper.ToDto(
            salesMode,
            packageSize,
            packageLabel,
            packageDescription,
            quantityUnitLabel,
            showTotalPieces,
            totalPieces,
            equivalentUnitPrice,
            salesDisplaySummary);
}

public sealed class GetAdminProductDemandOrdersQueryHandler(
    IAdminDemandReportReadModel readModel,
    IAdminOrderPixPaymentReader pixPaymentReader)
    : IQueryHandler<GetAdminProductDemandOrdersQuery, PagedAdminProductDemandOrdersDto>
{
    public async Task<PagedAdminProductDemandOrdersDto> Handle(
        GetAdminProductDemandOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var page = await readModel.GetProductDemandOrdersAsync(
            new AdminDemandOrdersQuerySpec(
                query.SkuId,
                query.Page,
                query.PageSize,
                query.From,
                query.To,
                AdminDemandReportSort.Normalize(query.Sort, AdminDemandReportSort.PaidAtDesc)),
            cancellationToken);

        var orderIds = page.Items.Select(x => x.OrderId).ToList();
        var payments = orderIds.Count == 0
            ? new Dictionary<Guid, AdminOrderPaymentSummaryDto>()
            : await pixPaymentReader.GetLatestByOrderIdsAsync(orderIds, cancellationToken);

        var items = page.Items.Select(row => new AdminProductDemandOrderDto(
            row.OrderId,
            FormatOrderNumber(row.OrderNumber, row.OrderId),
            row.CreatedAt,
            row.PaidAt,
            row.Status.ToString(),
            row.CustomerUserId is null,
            row.CustomerFullName,
            row.CustomerEmail,
            row.CustomerPhone,
            AdminReportContact.PhoneDigits(row.CustomerPhone),
            query.SkuId,
            row.ProductName,
            row.SkuCode,
            row.Quantity,
            row.UnitPrice,
            row.Subtotal,
            GetAdminProductDemandQueryHandler.Sales(
                row.SalesMode,
                row.PackageSize,
                row.PackageLabel,
                row.PackageDescription,
                row.QuantityUnitLabel,
                row.ShowTotalPieces,
                row.TotalPieces,
                row.EquivalentUnitPrice,
                row.SalesDisplaySummary),
            payments.TryGetValue(row.OrderId, out var payment) ? payment : null)).ToList();

        return new PagedAdminProductDemandOrdersDto(
            items,
            query.Page,
            query.PageSize,
            page.TotalItems,
            GetAdminProductDemandQueryHandler.TotalPages(page.TotalItems, query.PageSize));
    }

    internal static string FormatOrderNumber(long orderNumber, Guid orderId)
        => orderNumber > 0 ? orderNumber.ToString() : orderId.ToString();
}

public sealed class GetAdminUnpaidOrdersQueryHandler(
    IAdminDemandReportReadModel readModel,
    IAdminOrderPixPaymentReader pixPaymentReader)
    : IQueryHandler<GetAdminUnpaidOrdersQuery, PagedAdminUnpaidOrdersDto>
{
    public async Task<PagedAdminUnpaidOrdersDto> Handle(
        GetAdminUnpaidOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var (searchText, orderOrSkuId, orderNumber) = ParseSearch(query.Q);
        var paidPixOrderIds = await pixPaymentReader.FindOrderIdsByLatestPaymentStatusAsync(
            "Paid",
            cancellationToken);

        var sort = AdminDemandReportSort.Normalize(query.Sort, AdminDemandReportSort.CreatedAtDesc);
        var page = await readModel.GetUnpaidOrdersAsync(
            new AdminUnpaidOrdersQuerySpec(
                query.Page,
                query.PageSize,
                query.From,
                query.To,
                searchText,
                orderOrSkuId,
                orderNumber,
                string.Equals(sort, AdminDemandReportSort.CreatedAtAsc, StringComparison.OrdinalIgnoreCase),
                paidPixOrderIds),
            cancellationToken);

        var orderIds = page.Items.Select(x => x.Id).ToList();
        var payments = orderIds.Count == 0
            ? new Dictionary<Guid, AdminOrderPaymentSummaryDto>()
            : await pixPaymentReader.GetLatestByOrderIdsAsync(orderIds, cancellationToken);

        var items = page.Items.Select(row => new AdminUnpaidOrderDto(
            row.Id,
            GetAdminProductDemandOrdersQueryHandler.FormatOrderNumber(row.OrderNumber, row.Id),
            row.CreatedAt,
            row.Status.ToString(),
            row.CustomerUserId is null,
            row.CustomerFullName,
            row.CustomerEmail,
            row.CustomerPhone,
            AdminReportContact.PhoneDigits(row.CustomerPhone),
            row.Subtotal,
            row.ShippingAmount,
            row.Total,
            payments.TryGetValue(row.Id, out var payment) ? payment : null,
            row.Items.Select(item => new AdminUnpaidOrderItemDto(
                item.Id,
                item.SkuId,
                item.SkuCode,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.Subtotal,
                GetAdminProductDemandQueryHandler.Sales(
                    item.SalesMode,
                    item.PackageSize,
                    item.PackageLabel,
                    item.PackageDescription,
                    item.QuantityUnitLabel,
                    item.ShowTotalPieces,
                    item.TotalPieces,
                    item.EquivalentUnitPrice,
                    item.SalesDisplaySummary))).ToList())).ToList();

        return new PagedAdminUnpaidOrdersDto(
            items,
            query.Page,
            query.PageSize,
            page.TotalItems,
            GetAdminProductDemandQueryHandler.TotalPages(page.TotalItems, query.PageSize));
    }

    /// <summary>
    /// Mirrors admin order search: a short number is an order number, a phone-length
    /// digit string stays text, and a guid matches the order id or a SKU id.
    /// </summary>
    internal static (string? SearchText, Guid? OrderOrSkuId, long? OrderNumber) ParseSearch(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return (null, null, null);

        var raw = q.Trim();
        var forceOrderNumber = raw.StartsWith('#');
        var value = forceOrderNumber ? raw.TrimStart('#') : raw;
        if (string.IsNullOrWhiteSpace(value))
            return (null, null, null);

        if (!forceOrderNumber && Guid.TryParse(value, out var id) && id != Guid.Empty)
            return (null, id, null);

        if (long.TryParse(value, out var orderNumber)
            && orderNumber > 0
            && (forceOrderNumber || value.Length <= 9))
            return (null, null, orderNumber);

        return (value, null, null);
    }
}
