using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Interfaces;

/// <summary>
/// Cross-module read of Pix payment summaries for Admin Orders (implemented by PaymentsPix).
/// Always uses the latest payment per OrderId (CreatedAt desc). Never exposes QR/copy-paste/secrets.
/// </summary>
public interface IAdminOrderPixPaymentReader
{
    Task<IReadOnlyDictionary<Guid, AdminOrderPaymentSummaryDto>> GetLatestByOrderIdsAsync(
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken);

    Task<AdminOrderPaymentSummaryDto?> GetLatestByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Order ids whose latest Pix payment status equals <paramref name="paymentStatus"/> (enum name, case-insensitive).
    /// </summary>
    Task<IReadOnlyList<Guid>> FindOrderIdsByLatestPaymentStatusAsync(
        string paymentStatus,
        CancellationToken cancellationToken);
}
