using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Repositories;

public sealed class MercadoPagoWebhookEventRepository(PaymentsPixDbContext db)
    : IMercadoPagoWebhookEventRepository
{
    public async Task AddAsync(MercadoPagoWebhookEvent webhookEvent, CancellationToken cancellationToken)
        => await db.MercadoPagoWebhookEvents.AddAsync(webhookEvent, cancellationToken);

    public Task<MercadoPagoWebhookEvent?> GetByProviderEventIdAsync(
        string providerEventId,
        CancellationToken cancellationToken)
        => db.MercadoPagoWebhookEvents.FirstOrDefaultAsync(
            e => e.ProviderEventId == providerEventId,
            cancellationToken);
}
