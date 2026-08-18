using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Infrastructure.Mappings;

internal sealed class DeliveryBatchMap : IEntityTypeConfiguration<DeliveryBatch>
{
    public void Configure(EntityTypeBuilder<DeliveryBatch> map)
    {
        map.ToTable("delivery_batches");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.BatchNumber).IsRequired();
        map.HasIndex(x => x.BatchNumber)
            .IsUnique()
            .HasDatabaseName("IX_delivery_batches_BatchNumber");

        map.Property(x => x.CustomerUserId);
        map.Property(x => x.CustomerName).HasMaxLength(200);
        map.Property(x => x.CustomerEmail).HasMaxLength(320);
        map.Property(x => x.CustomerEmailNormalized).HasMaxLength(320);
        map.Property(x => x.CustomerPhone).HasMaxLength(30);
        map.Property(x => x.CustomerPhoneNormalized).HasMaxLength(30);

        map.Property(x => x.DeliveryMethod)
            .HasConversion<string>()
            .HasMaxLength(30);

        map.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        map.Property(x => x.TrackingCode).HasMaxLength(120);
        map.Property(x => x.InternalNote).HasMaxLength(2000);
        map.Property(x => x.ShippedAt);
        map.Property(x => x.DeliveredAt);
        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.CreatedByAdminId);
        map.Property(x => x.UpdatedAt);
        map.Property(x => x.UpdatedByAdminId);
        map.Property(x => x.HasDifferentDeliveryAddresses).IsRequired();

        map.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_delivery_batches_Status_CreatedAt");
        map.HasIndex(x => new { x.CustomerUserId, x.Status })
            .HasDatabaseName("IX_delivery_batches_CustomerUserId_Status");
        map.HasIndex(x => new { x.CustomerEmailNormalized, x.Status })
            .HasDatabaseName("IX_delivery_batches_CustomerEmailNormalized_Status");

        map.HasMany(x => x.Orders)
            .WithOne()
            .HasForeignKey(x => x.DeliveryBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        map.Navigation(x => x.Orders).UsePropertyAccessMode(PropertyAccessMode.Field);

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}

internal sealed class DeliveryBatchOrderMap : IEntityTypeConfiguration<DeliveryBatchOrder>
{
    public void Configure(EntityTypeBuilder<DeliveryBatchOrder> map)
    {
        map.ToTable("delivery_batch_orders");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.DeliveryBatchId).IsRequired();
        map.Property(x => x.OrderId).IsRequired();
        map.Property(x => x.CreatedAt).IsRequired();

        map.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasDatabaseName("IX_delivery_batch_orders_OrderId");
        map.HasIndex(x => x.DeliveryBatchId)
            .HasDatabaseName("IX_delivery_batch_orders_DeliveryBatchId");

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}
