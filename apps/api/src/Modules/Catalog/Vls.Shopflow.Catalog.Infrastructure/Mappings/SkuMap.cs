using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.Enums;

namespace Vls.Shopflow.Catalog.Infrastructure.Mappings;

internal sealed class SkuMap : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> map)
    {
        map.ToTable("product_skus", t =>
        {
            t.AddPriceConstraints("", "product_skus");
            t.HasCheckConstraint(
                "CK_product_skus_minimum_quantity",
                "minimum_quantity >= 1");
            t.HasCheckConstraint(
                "CK_product_skus_quantity_step",
                "quantity_step >= 1");
            t.HasCheckConstraint(
                "CK_product_skus_package_size",
                "package_size IS NULL OR package_size > 1");
        });
        map.HasKey(s => s.Id);

        map.Property(s => s.Id).ValueGeneratedNever();

        map.Property(s => s.ProductId).IsRequired();
        map.Property(s => s.Code).HasMaxLength(128).IsRequired();
        map.Property(s => s.IsActive);

        map.HasIndex(s => new { s.ProductId, s.Code }).IsUnique();

        map.Ignore("_events");
        map.Ignore(s => s.DomainEvents);

        map.OwnsOne(s => s.Price, owned =>
        {
            owned.MapPrice("");
        });

        map.OwnsOne(s => s.SalesRule, owned =>
        {
            owned.Property(r => r.SalesMode)
                .HasColumnName("sales_mode")
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(SalesMode.Unit);

            owned.Property(r => r.MinimumQuantity)
                .HasColumnName("minimum_quantity")
                .IsRequired()
                .HasDefaultValue(1);

            owned.Property(r => r.QuantityStep)
                .HasColumnName("quantity_step")
                .IsRequired()
                .HasDefaultValue(1);

            owned.Property(r => r.PackageSize)
                .HasColumnName("package_size");

            owned.Property(r => r.PackageLabel)
                .HasColumnName("package_label")
                .HasMaxLength(200);

            owned.Property(r => r.PackageDescription)
                .HasColumnName("package_description")
                .HasMaxLength(1000);

            owned.Property(r => r.QuantityUnitLabel)
                .HasColumnName("quantity_unit_label")
                .HasMaxLength(64);

            owned.Property(r => r.AllowCustomerToChooseVariants)
                .HasColumnName("allow_customer_to_choose_variants")
                .IsRequired()
                .HasDefaultValue(true);

            owned.Property(r => r.ShowTotalPieces)
                .HasColumnName("show_total_pieces")
                .IsRequired()
                .HasDefaultValue(false);

            owned.Property(r => r.IsWholesaleOnly)
                .HasColumnName("is_wholesale_only")
                .IsRequired()
                .HasDefaultValue(false);
        });

        map.Navigation(s => s.SalesRule).IsRequired();

        map.HasMany<SkuAttribute>(s => s.Attributes)
            .WithOne(a => a.Sku)
            .HasForeignKey(a => a.SkuId)
            .OnDelete(DeleteBehavior.Cascade);

        map.Navigation(s => s.Attributes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
