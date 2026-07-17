using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Interfaces;

/// <summary>
/// Thin Pix summary for customer account area. Implemented by PaymentsPix.
/// Latest payment per OrderId (CreatedAt desc). Never exposes provider IDs or QR data.
/// </summary>
public interface ICustomerOrderPixPaymentReader
{
    Task<IReadOnlyDictionary<Guid, CustomerOrderPaymentSummaryDto>> GetLatestByOrderIdsAsync(
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken);

    Task<CustomerOrderPaymentSummaryDto?> GetLatestByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> FindOrderIdsByLatestPaymentStatusAsync(
        string paymentStatus,
        CancellationToken cancellationToken);
}
