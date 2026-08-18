using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

/// <summary>
/// Fallback when PaymentsPix module is not registered (e.g. isolated Orders tests).
/// </summary>
public sealed class NullOrderPixPaymentStatusReader : IOrderPixPaymentStatusReader
{
    public Task<OrderPixPaymentStatusSnapshot?> GetLatestByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
        => Task.FromResult<OrderPixPaymentStatusSnapshot?>(null);
}
