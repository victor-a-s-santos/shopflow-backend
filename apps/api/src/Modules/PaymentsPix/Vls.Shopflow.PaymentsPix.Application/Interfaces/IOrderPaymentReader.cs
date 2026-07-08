namespace Vls.Shopflow.PaymentsPix.Application.Interfaces;

public sealed record OrderPaymentSnapshot(
    Guid OrderId,
    string Status,
    decimal Total,
    string CustomerFullName,
    string CustomerEmail);

public interface IOrderPaymentReader
{
    Task<OrderPaymentSnapshot?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);
}
