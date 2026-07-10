using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Application.Repositories;

public interface IPixPaymentRepository
{
    Task AddAsync(PixPayment payment, CancellationToken cancellationToken);

    Task<PixPayment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<PixPayment?> GetByProviderPaymentIdAsync(string providerPaymentId, CancellationToken cancellationToken);

    Task<PixPayment?> GetByProviderOrderIdAsync(string providerOrderId, CancellationToken cancellationToken);

    Task<PixPayment?> GetPendingByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<PixPayment?> GetLatestByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PixPayment>> GetExpiredPendingBatchAsync(
        DateTimeOffset asOfUtc,
        DateTimeOffset createdBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken);
}

public interface IMercadoPagoWebhookEventRepository
{
    Task AddAsync(MercadoPagoWebhookEvent webhookEvent, CancellationToken cancellationToken);

    Task<MercadoPagoWebhookEvent?> GetByProviderEventIdAsync(
        string providerEventId,
        CancellationToken cancellationToken);
}

public interface IPaymentsPixUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
