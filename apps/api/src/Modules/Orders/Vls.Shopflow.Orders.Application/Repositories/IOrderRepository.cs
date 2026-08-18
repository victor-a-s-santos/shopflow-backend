using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Repositories;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order?> GetByOrderNumberWithItemsAsync(long orderNumber, CancellationToken cancellationToken);

    Task<Order?> GetByCheckoutSessionIdWithItemsAsync(Guid checkoutSessionId, CancellationToken cancellationToken);

    Task<bool> ExistsByCheckoutSessionIdAsync(Guid checkoutSessionId, CancellationToken cancellationToken);

    Task<Order?> GetPendingPaymentByCheckoutSessionIdAsync(
        Guid checkoutSessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> GetByIdsWithItemsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Paid + AwaitingShipment orders for the same customer (by user id or email+phone).
    /// </summary>
    Task<IReadOnlyList<Order>> FindEligibleGroupingCandidatesAsync(
        Guid? customerUserId,
        string? emailNormalized,
        string? phoneNormalized,
        CancellationToken cancellationToken);
}

public interface IGuestOrderAccessTokenRepository
{
    Task AddAsync(GuestOrderAccessToken token, CancellationToken cancellationToken);

    Task<GuestOrderAccessToken?> FindActiveByOrderIdAndHashAsync(
        Guid orderId,
        string tokenHash,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds a non-revoked token by order + hash regardless of expiry (for expired vs invalid distinction).
    /// </summary>
    Task<GuestOrderAccessToken?> FindByOrderIdAndHashAsync(
        Guid orderId,
        string tokenHash,
        CancellationToken cancellationToken);
}

public interface IOrdersUnitOfWork : IUnitOfWork;
