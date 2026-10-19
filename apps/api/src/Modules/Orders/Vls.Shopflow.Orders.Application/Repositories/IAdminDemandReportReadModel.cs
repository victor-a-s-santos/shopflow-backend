using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Repositories;

public sealed record AdminDemandSummaryQuerySpec(
    int Page,
    int PageSize,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? SearchText,
    Guid? SearchSkuId,
    string Sort);

public sealed record AdminDemandSummaryRow(
    Guid SkuId,
    string ProductName,
    string SkuCode,
    int TotalQuantity,
    int OrderCount,
    decimal SubtotalTotal,
    string? SalesMode,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool? ShowTotalPieces,
    int? TotalPieces,
    decimal? EquivalentUnitPrice,
    string? SalesDisplaySummary);

public sealed record AdminDemandSummaryPage(
    IReadOnlyList<AdminDemandSummaryRow> Items,
    int TotalItems);

public sealed record AdminDemandOrdersQuerySpec(
    Guid SkuId,
    int Page,
    int PageSize,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string Sort);

public sealed record AdminDemandOrderRow(
    Guid OrderId,
    long OrderNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    OrderStatus Status,
    Guid? CustomerUserId,
    string CustomerFullName,
    string CustomerEmail,
    string CustomerPhone,
    int Quantity,
    decimal? UnitPrice,
    decimal Subtotal,
    string ProductName,
    string SkuCode,
    string? SalesMode,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool? ShowTotalPieces,
    int? TotalPieces,
    decimal? EquivalentUnitPrice,
    string? SalesDisplaySummary);

public sealed record AdminDemandOrdersPage(
    IReadOnlyList<AdminDemandOrderRow> Items,
    int TotalItems);

public sealed record AdminUnpaidOrdersQuerySpec(
    int Page,
    int PageSize,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? SearchText,
    Guid? SearchOrderOrSkuId,
    long? SearchOrderNumber,
    bool SortCreatedAtAscending,
    IReadOnlyList<Guid>? ExcludeOrderIds);

public sealed record AdminUnpaidOrderItemRow(
    Guid Id,
    Guid SkuId,
    string SkuCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    string? SalesMode,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool? ShowTotalPieces,
    int? TotalPieces,
    decimal? EquivalentUnitPrice,
    string? SalesDisplaySummary);

public sealed record AdminUnpaidOrderRow(
    Guid Id,
    long OrderNumber,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    Guid? CustomerUserId,
    string CustomerFullName,
    string CustomerEmail,
    string CustomerPhone,
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total,
    IReadOnlyList<AdminUnpaidOrderItemRow> Items);

public sealed record AdminUnpaidOrdersPage(
    IReadOnlyList<AdminUnpaidOrderRow> Items,
    int TotalItems);

public interface IAdminDemandReportReadModel
{
    Task<AdminDemandSummaryPage> GetProductDemandAsync(
        AdminDemandSummaryQuerySpec spec,
        CancellationToken cancellationToken);

    Task<AdminDemandOrdersPage> GetProductDemandOrdersAsync(
        AdminDemandOrdersQuerySpec spec,
        CancellationToken cancellationToken);

    Task<AdminUnpaidOrdersPage> GetUnpaidOrdersAsync(
        AdminUnpaidOrdersQuerySpec spec,
        CancellationToken cancellationToken);
}
