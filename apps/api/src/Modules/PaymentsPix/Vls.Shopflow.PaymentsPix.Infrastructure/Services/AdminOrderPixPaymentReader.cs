using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Services;

public sealed class AdminOrderPixPaymentReader(PaymentsPixDbContext db) : IAdminOrderPixPaymentReader
{
    public async Task<IReadOnlyDictionary<Guid, AdminOrderPaymentSummaryDto>> GetLatestByOrderIdsAsync(
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
            return new Dictionary<Guid, AdminOrderPaymentSummaryDto>();

        var distinctIds = orderIds.Distinct().ToList();

        var payments = await db.PixPayments
            .AsNoTracking()
            .Where(p => distinctIds.Contains(p.OrderId))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var map = new Dictionary<Guid, AdminOrderPaymentSummaryDto>();
        foreach (var payment in payments)
        {
            if (map.ContainsKey(payment.OrderId))
                continue;

            map[payment.OrderId] = ToSummary(payment);
        }

        return map;
    }

    public async Task<AdminOrderPaymentSummaryDto?> GetLatestByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var payment = await db.PixPayments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return payment is null ? null : ToSummary(payment);
    }

    public async Task<IReadOnlyList<Guid>> FindOrderIdsByLatestPaymentStatusAsync(
        string paymentStatus,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PixPaymentStatus>(paymentStatus.Trim(), ignoreCase: true, out var status))
            return [];

        // Latest payment per order = row whose CreatedAt equals Max(CreatedAt) for that OrderId.
        var orderIds = await db.PixPayments
            .AsNoTracking()
            .Where(p =>
                p.Status == status
                && p.CreatedAt == db.PixPayments
                    .Where(x => x.OrderId == p.OrderId)
                    .Max(x => x.CreatedAt))
            .Select(p => p.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return orderIds;
    }

    private static AdminOrderPaymentSummaryDto ToSummary(Domain.Entities.PixPayment payment)
        => new(
            payment.Id,
            payment.Provider.ToString(),
            payment.Status.ToString(),
            payment.ProviderOrderId,
            payment.ProviderPaymentId ?? payment.ProviderTransactionId,
            payment.ProviderTransactionId,
            payment.ProviderStatus,
            payment.ProviderStatusDetail,
            payment.ProviderTransactionStatus,
            payment.ProviderTransactionStatusDetail,
            payment.ProviderApprovedAt,
            payment.ProviderUpdatedAt,
            payment.PaidAt,
            payment.ExpiresAt);
}
