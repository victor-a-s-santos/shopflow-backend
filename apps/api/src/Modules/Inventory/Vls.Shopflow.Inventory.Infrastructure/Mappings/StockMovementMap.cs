using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Inventory.Domain.Entities;

namespace Vls.Shopflow.Inventory.Infrastructure.Mappings;

internal sealed class StockMovementMap : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> map)
    {
        map.ToTable("stock_movements");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.InventoryItemId).IsRequired();
        map.Property(x => x.SkuId).IsRequired();
        map.Property(x => x.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        map.Property(x => x.Quantity).IsRequired();
        map.Property(x => x.Reason).HasMaxLength(500);
        map.Property(x => x.CreatedAt).IsRequired();

        map.HasIndex(x => x.SkuId);
        map.HasIndex(x => x.InventoryItemId);
        map.HasIndex(x => x.CreatedAt);

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}
