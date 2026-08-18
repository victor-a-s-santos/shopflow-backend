using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.PaymentsPix.Domain.Entities;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Mappings;

internal sealed class MercadoPagoWebhookEventMap : IEntityTypeConfiguration<MercadoPagoWebhookEvent>
{
    public void Configure(EntityTypeBuilder<MercadoPagoWebhookEvent> map)
    {
        map.ToTable("mercado_pago_webhook_events");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.ProviderEventId).HasMaxLength(100);
        map.HasIndex(x => x.ProviderEventId)
            .IsUnique()
            .HasFilter("\"ProviderEventId\" IS NOT NULL");

        map.Property(x => x.ProviderOrderId).HasMaxLength(200).IsRequired();
        map.HasIndex(x => x.ProviderOrderId);

        map.Property(x => x.RequestId).HasMaxLength(100);
        map.Property(x => x.Action).HasMaxLength(100);
        map.Property(x => x.Type).HasMaxLength(50);
        map.Property(x => x.ProcessingStatus).HasMaxLength(30).IsRequired();
        map.Property(x => x.ErrorMessage).HasMaxLength(500);
        map.Property(x => x.ReceivedAt).IsRequired();
        map.Property(x => x.ProcessedAt);
        map.Property(x => x.LiveMode).IsRequired();
        map.Property(x => x.SignatureValid).IsRequired();
    }
}
