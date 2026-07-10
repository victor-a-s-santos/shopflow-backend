using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.PaymentsPix.Domain.Entities;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Mappings;

internal sealed class PixPaymentMap : IEntityTypeConfiguration<PixPayment>
{
    public void Configure(EntityTypeBuilder<PixPayment> map)
    {
        map.ToTable("pix_payments");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.OrderId).IsRequired();
        map.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");

        map.Property(x => x.Amount)
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        map.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        map.Property(x => x.Provider)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        map.Property(x => x.ProviderPaymentId).HasMaxLength(200);
        map.HasIndex(x => x.ProviderPaymentId)
            .HasFilter("\"ProviderPaymentId\" IS NOT NULL");

        map.Property(x => x.ProviderOrderId).HasMaxLength(200);
        map.HasIndex(x => x.ProviderOrderId)
            .HasFilter("\"ProviderOrderId\" IS NOT NULL");

        map.Property(x => x.ProviderTransactionId).HasMaxLength(200);
        map.HasIndex(x => x.ProviderTransactionId)
            .HasFilter("\"ProviderTransactionId\" IS NOT NULL");

        map.Property(x => x.ProviderStatus).HasMaxLength(50);
        map.Property(x => x.ProviderStatusDetail).HasMaxLength(100);
        map.Property(x => x.ProviderTransactionStatus).HasMaxLength(50);
        map.Property(x => x.ProviderTransactionStatusDetail).HasMaxLength(100);
        map.Property(x => x.ExternalReference).HasMaxLength(150);
        map.Property(x => x.IdempotencyKey).HasMaxLength(64);

        map.Property(x => x.QrCode).HasMaxLength(20000);
        map.Property(x => x.QrCodeImageUrl).HasMaxLength(2000);
        map.Property(x => x.CopyPasteCode).HasMaxLength(2000);
        map.Property(x => x.TicketUrl).HasMaxLength(2000);
        map.Property(x => x.FailureReason).HasMaxLength(500);

        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.ExpiresAt);
        map.Property(x => x.PaidAt);
        map.Property(x => x.ProviderApprovedAt);
        map.Property(x => x.ProviderUpdatedAt);
        map.Property(x => x.CanceledAt);
        map.Property(x => x.FailedAt);

        map.ToTable(t => t.HasCheckConstraint("CK_pix_payments_amount_positive", "\"Amount\" > 0"));

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}
