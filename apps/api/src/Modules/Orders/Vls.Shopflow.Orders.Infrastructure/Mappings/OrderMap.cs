using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Infrastructure.Mappings;

internal sealed class OrderMap : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> map)
    {
        map.ToTable("orders");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.CheckoutSessionId).IsRequired();
        map.HasIndex(x => x.CheckoutSessionId).IsUnique();

        map.Property(x => x.CustomerFullName).HasMaxLength(200).IsRequired();
        map.Property(x => x.CustomerEmail).HasMaxLength(320).IsRequired();
        map.Property(x => x.CustomerPhone).HasMaxLength(30).IsRequired();
        map.Property(x => x.ShippingZipCode).HasMaxLength(20).IsRequired();
        map.Property(x => x.ShippingStreet).HasMaxLength(200).IsRequired();
        map.Property(x => x.ShippingNumber).HasMaxLength(30).IsRequired();
        map.Property(x => x.ShippingComplement).HasMaxLength(120);
        map.Property(x => x.ShippingNeighborhood).HasMaxLength(120).IsRequired();
        map.Property(x => x.ShippingCity).HasMaxLength(120).IsRequired();
        map.Property(x => x.ShippingState).HasMaxLength(2).IsRequired();

        map.Property(x => x.Subtotal).HasColumnType("numeric(12,2)").IsRequired();
        map.Property(x => x.ShippingAmount).HasColumnType("numeric(12,2)");
        map.Property(x => x.Total).HasColumnType("numeric(12,2)").IsRequired();

        map.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        map.Property(x => x.CustomerUserId);

        map.Property(x => x.OrderNumber).IsRequired();
        map.HasIndex(x => x.OrderNumber)
            .IsUnique()
            .HasDatabaseName("IX_orders_OrderNumber");

        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.UpdatedAt);
        map.Property(x => x.PaidAt);
        map.Property(x => x.CanceledAt);

        map.HasIndex(x => x.CustomerEmail);
        map.HasIndex(x => x.CreatedAt);
        map.HasIndex(x => new { x.CustomerUserId, x.CreatedAt })
            .HasDatabaseName("IX_orders_CustomerUserId_CreatedAt");

        map.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        map.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}

internal sealed class OrderItemMap : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> map)
    {
        map.ToTable("order_items");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.OrderId).IsRequired();
        map.Property(x => x.SkuId).IsRequired();
        map.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        map.Property(x => x.SkuCode).HasMaxLength(128).IsRequired();
        map.Property(x => x.Quantity).IsRequired();
        map.Property(x => x.UnitPrice).HasColumnType("numeric(12,2)").IsRequired();
        map.Property(x => x.Subtotal).HasColumnType("numeric(12,2)").IsRequired();

        map.HasIndex(x => x.OrderId);
        map.HasIndex(x => x.SkuId);
    }
}
