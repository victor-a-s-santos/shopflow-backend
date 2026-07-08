using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Enums;

namespace Vls.Shopflow.CartCheckout.Infrastructure.Repositories;

public sealed class CheckoutSessionRepository(CartCheckoutDbContext db) : ICheckoutSessionRepository
{
    public async Task AddAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        await db.CheckoutSessions.AddAsync(session, cancellationToken);
    }

    public Task<CheckoutSession?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken)
        => db.CheckoutSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CheckoutSession>> GetExpiredPendingBatchAsync(
        DateTimeOffset asOfUtc,
        int batchSize,
        CancellationToken cancellationToken)
        => await db.CheckoutSessions
            .Include(s => s.Items)
            .Where(s =>
                s.Status == CheckoutSessionStatus.Pending &&
                s.ReservationExpiresAt <= asOfUtc)
            .OrderBy(s => s.ReservationExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
}