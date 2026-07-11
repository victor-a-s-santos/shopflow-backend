using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Repositories;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order?> GetByCheckoutSessionIdWithItemsAsync(Guid checkoutSessionId, CancellationToken cancellationToken);

    Task<bool> ExistsByCheckoutSessionIdAsync(Guid checkoutSessionId, CancellationToken cancellationToken);

    Task<Order?> GetPendingPaymentByCheckoutSessionIdAsync(
        Guid checkoutSessionId,
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
}

public interface IOrdersUnitOfWork : IUnitOfWork;
