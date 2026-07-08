using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Inventory.Domain.Entities;

namespace Vls.Shopflow.Inventory.Infrastructure.Mappings;

internal sealed class StockReservationMap : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> map)
    {
        map.ToTable("stock_reservations");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.InventoryItemId).IsRequired();
        map.Property(x => x.SkuId).IsRequired();
        map.Property(x => x.Quantity).IsRequired();
        map.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.ConfirmedAt);
        map.Property(x => x.CanceledAt);
        map.Property(x => x.ExpiresAt);

        map.HasIndex(x => x.SkuId);
        map.HasIndex(x => x.InventoryItemId);
        map.HasIndex(x => x.Status);

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}
