using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.CartCheckout.Domain.Entities;

namespace Vls.Shopflow.CartCheckout.Infrastructure.Mappings;

internal sealed class CheckoutSessionMap : IEntityTypeConfiguration<CheckoutSession>
{
    public void Configure(EntityTypeBuilder<CheckoutSession> map)
    {
        map.ToTable("checkout_sessions");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        map.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        map.Property(x => x.CustomerEmail).HasMaxLength(320).IsRequired();
        map.Property(x => x.CustomerPhone).HasMaxLength(30).IsRequired();
        map.Property(x => x.AddressZipCode).HasMaxLength(20).IsRequired();
        map.Property(x => x.AddressStreet).HasMaxLength(200).IsRequired();
        map.Property(x => x.AddressNumber).HasMaxLength(30).IsRequired();
        map.Property(x => x.AddressComplement).HasMaxLength(120);
        map.Property(x => x.AddressNeighborhood).HasMaxLength(120).IsRequired();
        map.Property(x => x.AddressCity).HasMaxLength(120).IsRequired();
        map.Property(x => x.AddressState).HasMaxLength(2).IsRequired();

        map.Property(x => x.Subtotal).HasColumnType("numeric(12,2)").IsRequired();
        map.Property(x => x.ShippingAmount).HasColumnType("numeric(12,2)");
        map.Property(x => x.Total).HasColumnType("numeric(12,2)").IsRequired();
        map.Property(x => x.ReservationExpiresAt).IsRequired();
        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.UpdatedAt).IsRequired();
        map.Property(x => x.CanceledAt);

        map.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.CheckoutSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        map.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}

internal sealed class CheckoutSessionItemMap : IEntityTypeConfiguration<CheckoutSessionItem>
{
    public void Configure(EntityTypeBuilder<CheckoutSessionItem> map)
    {
        map.ToTable("checkout_session_items");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.CheckoutSessionId).IsRequired();
        map.Property(x => x.ProductId).IsRequired();
        map.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        map.Property(x => x.ProductSlug).HasMaxLength(200).IsRequired();
        map.Property(x => x.SkuId).IsRequired();
        map.Property(x => x.SkuCode).HasMaxLength(128).IsRequired();
        map.Property(x => x.Quantity).IsRequired();
        map.Property(x => x.UnitPrice).HasColumnType("numeric(12,2)").IsRequired();
        map.Property(x => x.Subtotal).HasColumnType("numeric(12,2)").IsRequired();
        map.Property(x => x.InventoryReservationId).IsRequired();

        map.Property(x => x.SalesMode).HasMaxLength(32);
        map.Property(x => x.PackageSize);
        map.Property(x => x.PackageLabel).HasMaxLength(200);
        map.Property(x => x.PackageDescription).HasMaxLength(1000);
        map.Property(x => x.QuantityUnitLabel).HasMaxLength(64);
        map.Property(x => x.ShowTotalPieces);
        map.Property(x => x.TotalPieces);
        map.Property(x => x.EquivalentUnitPrice).HasColumnType("numeric(12,2)");
        map.Property(x => x.SalesDisplaySummary).HasMaxLength(200);

        map.HasIndex(x => x.CheckoutSessionId);
        map.HasIndex(x => x.SkuId);
        map.HasIndex(x => x.InventoryReservationId);
    }
}
