using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Repositories;

public sealed record DeliveryBatchMembership(
    Guid OrderId,
    Guid DeliveryBatchId,
    long BatchNumber);

public sealed record DeliveryBatchListQuerySpec(
    int Page,
    int PageSize,
    DeliveryBatchStatus? Status,
    string? SearchText,
    long? SearchBatchNumber,
    long? SearchOrderNumber,
    string? CustomerEmail,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    bool SortCreatedAtAscending = false);

public sealed record DeliveryBatchListRow(
    Guid Id,
    long BatchNumber,
    DeliveryBatchStatus Status,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    int OrderCount,
    decimal TotalAmount,
    DeliveryMethod? DeliveryMethod,
    string? TrackingCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt,
    bool HasDifferentDeliveryAddresses);

public sealed record DeliveryBatchListPage(
    IReadOnlyList<DeliveryBatchListRow> Items,
    int TotalItems);

public interface IDeliveryBatchRepository
{
    Task AddAsync(DeliveryBatch batch, CancellationToken cancellationToken);

    Task<DeliveryBatch?> GetByIdWithOrdersAsync(Guid batchId, CancellationToken cancellationToken);

    Task<bool> IsOrderInAnyBatchAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> GetOrderIdsInAnyBatchAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken);

    Task<DeliveryBatchMembership?> FindMembershipByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, DeliveryBatchMembership>> FindMembershipsByOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken);
}

public interface IDeliveryBatchReadModel
{
    Task<DeliveryBatchListPage> GetPagedAsync(
        DeliveryBatchListQuerySpec spec,
        CancellationToken cancellationToken);
}

public interface IDeliveryBatchNumberGenerator
{
    Task<long> NextAsync(CancellationToken cancellationToken = default);
}
