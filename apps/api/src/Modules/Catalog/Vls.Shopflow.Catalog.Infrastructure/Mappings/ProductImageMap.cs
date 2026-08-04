using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Infrastructure.Mappings;

internal sealed class ProductImageMap : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> map)
    {
        map.ToTable("product_images");
        map.HasKey(x => x.Id);

        map.Property(x => x.Url).HasMaxLength(500).IsRequired();
        map.Property(x => x.ObjectKey).HasMaxLength(500);
        map.Property(x => x.StorageProvider).HasMaxLength(50);
        map.Property(x => x.ContentType).HasMaxLength(100);
        map.Property(x => x.SizeBytes);
        map.Property(x => x.SortOrder);
        map.Property(x => x.IsPrimary);
        map.Property(x => x.CreatedAt);

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);

        map.HasOne(x => x.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        map.HasIndex(x => new { x.ProductId, x.SortOrder });
        map.HasIndex(x => new { x.ProductId, x.IsPrimary });
        map.HasIndex(x => x.ObjectKey);
    }
}
