using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

public sealed class NullCustomerOrderPixPaymentReader : ICustomerOrderPixPaymentReader
{
    public Task<IReadOnlyDictionary<Guid, CustomerOrderPaymentSummaryDto>> GetLatestByOrderIdsAsync(
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, CustomerOrderPaymentSummaryDto>>(
            new Dictionary<Guid, CustomerOrderPaymentSummaryDto>());

    public Task<CustomerOrderPaymentSummaryDto?> GetLatestByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
        => Task.FromResult<CustomerOrderPaymentSummaryDto?>(null);

    public Task<IReadOnlyList<Guid>> FindOrderIdsByLatestPaymentStatusAsync(
        string paymentStatus,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Guid>>([]);
}
