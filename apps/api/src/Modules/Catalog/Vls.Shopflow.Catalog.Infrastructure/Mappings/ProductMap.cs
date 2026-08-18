using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Infrastructure.Mappings;

internal sealed class ProductMap : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> map)
    {
        map.ToTable("products", t => t.AddPriceConstraints("base_", "products"));
        map.HasKey(p => p.Id);

        map.Property(p => p.Name).HasMaxLength(200).IsRequired();

        map.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(Product.MaxDescriptionLength);

        map.Property(p => p.IsActive);
        map.Property(p => p.HasSkus);
        map.Property(p => p.IsFeatured).IsRequired().HasDefaultValue(false);
        map.Property(p => p.DisplayOrder);
        map.Property(p => p.CreatedAt).IsRequired();

        // Storefront list default: featured → displayOrder → createdAt → id
        map.HasIndex(p => new { p.IsActive, p.IsFeatured, p.DisplayOrder, p.CreatedAt, p.Id })
            .HasDatabaseName("IX_products_storefront_list");

        // evitar mapear eventos
        map.Ignore("_events");
        map.Ignore(p => p.DomainEvents);

        // BasePrice (owned) com prefixo base_
        map.OwnsOne(p => p.BasePrice, owned =>
        {
            owned.MapPrice("base_");
        });
        
        map.OwnsOne(p => p.Slug, s =>
        {
            s.Property(x => x.Value)
                .HasColumnName("slug")
                .HasMaxLength(200)
                .IsRequired();

            // índice único no Value do Slug (na mesma tabela de products)
            s.HasIndex(x => x.Value).IsUnique();
        });

        // relação com SKUs
        map.HasMany(p => p.Skus)
            .WithOne(s => s.Product)
            .HasForeignKey(s => s.ProductId);

        map.Navigation(p => p.Skus)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}