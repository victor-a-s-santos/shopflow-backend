using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Inventory.Domain.Entities;

namespace Vls.Shopflow.Inventory.Infrastructure.Mappings;

internal sealed class InventoryItemMap : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> map)
    {
        map.ToTable("inventory_items", t =>
        {
            t.HasCheckConstraint("CK_inventory_items_on_hand_nonneg", "\"QuantityOnHand\" >= 0");
            t.HasCheckConstraint("CK_inventory_items_reserved_nonneg", "\"QuantityReserved\" >= 0");
            t.HasCheckConstraint(
                "CK_inventory_items_reserved_lte_on_hand",
                "\"QuantityReserved\" <= \"QuantityOnHand\"");
        });
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.SkuId).IsRequired();
        map.Property(x => x.QuantityOnHand).IsRequired();
        map.Property(x => x.QuantityReserved).IsRequired();
        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.UpdatedAt).IsRequired();

        map.HasIndex(x => x.SkuId).IsUnique();

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
        map.Ignore(x => x.AvailableQuantity);

        map.HasMany(x => x.Movements)
            .WithOne(m => m.InventoryItem)
            .HasForeignKey(m => m.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);

        map.HasMany(x => x.Reservations)
            .WithOne(r => r.InventoryItem)
            .HasForeignKey(r => r.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);

        map.Navigation(x => x.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);
        map.Navigation(x => x.Reservations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
