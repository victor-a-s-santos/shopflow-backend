using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

public sealed class NullAdminOrderPixPaymentReader : IAdminOrderPixPaymentReader
{
    public Task<IReadOnlyDictionary<Guid, AdminOrderPaymentSummaryDto>> GetLatestByOrderIdsAsync(
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, AdminOrderPaymentSummaryDto>>(
            new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

    public Task<AdminOrderPaymentSummaryDto?> GetLatestByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
        => Task.FromResult<AdminOrderPaymentSummaryDto?>(null);

    public Task<IReadOnlyList<Guid>> FindOrderIdsByLatestPaymentStatusAsync(
        string paymentStatus,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Guid>>([]);
}
