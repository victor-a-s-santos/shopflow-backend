using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Domain.Entities;

namespace Vls.Shopflow.CartCheckout.Application.Repositories;

public interface ICheckoutSessionRepository
{
    Task AddAsync(CheckoutSession session, CancellationToken cancellationToken);

    Task<CheckoutSession?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CheckoutSession>> GetExpiredPendingBatchAsync(
        DateTimeOffset asOfUtc,
        int batchSize,
        CancellationToken cancellationToken);
}

public interface ICartCheckoutUnitOfWork : IUnitOfWork;
