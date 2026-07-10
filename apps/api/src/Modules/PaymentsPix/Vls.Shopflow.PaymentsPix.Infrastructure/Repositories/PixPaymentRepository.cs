using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Repositories;

public sealed class PixPaymentRepository(PaymentsPixDbContext db) : IPixPaymentRepository
{
    public async Task AddAsync(PixPayment payment, CancellationToken cancellationToken)
        => await db.PixPayments.AddAsync(payment, cancellationToken);

    public Task<PixPayment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken)
        => db.PixPayments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

    public Task<PixPayment?> GetByProviderPaymentIdAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
        => db.PixPayments.FirstOrDefaultAsync(
            p => p.ProviderPaymentId == providerPaymentId
                 || p.ProviderTransactionId == providerPaymentId,
            cancellationToken);

    public Task<PixPayment?> GetByProviderOrderIdAsync(
        string providerOrderId,
        CancellationToken cancellationToken)
        => db.PixPayments.FirstOrDefaultAsync(
            p => p.ProviderOrderId == providerOrderId,
            cancellationToken);

    public Task<PixPayment?> GetPendingByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
        => db.PixPayments.FirstOrDefaultAsync(
            p => p.OrderId == orderId && p.Status == PixPaymentStatus.Pending,
            cancellationToken);

    public Task<PixPayment?> GetLatestByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
        => db.PixPayments
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PixPayment>> GetExpiredPendingBatchAsync(
        DateTimeOffset asOfUtc,
        DateTimeOffset createdBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken)
        => await db.PixPayments
            .Where(p =>
                p.Status == PixPaymentStatus.Pending &&
                ((p.ExpiresAt != null && p.ExpiresAt <= asOfUtc) ||
                 (p.ExpiresAt == null && p.CreatedAt <= createdBeforeUtc)))
            .OrderBy(p => p.ExpiresAt ?? p.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
}
