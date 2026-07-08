using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Infrastructure.Mappings;


internal sealed class SkuMap : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> map)
    {
        map.ToTable("product_skus", t => t.AddPriceConstraints("", "product_skus"));
        map.HasKey(s => s.Id);

        map.Property(s => s.Id).ValueGeneratedNever(); 
        
        map.Property(s => s.ProductId).IsRequired();
        map.Property(s => s.Code).HasMaxLength(128).IsRequired();
        map.Property(s => s.IsActive);

        map.HasIndex(s => new { s.ProductId, s.Code }).IsUnique();

        // evitar mapear eventos
        map.Ignore("_events");
        map.Ignore(s => s.DomainEvents);

        // Price (owned) SEM prefixo (colunas: regular_price, promo_price, etc.)
        map.OwnsOne(s => s.Price, owned =>
        {
            owned.MapPrice("");
        });

        map.HasMany<SkuAttribute>(s => s.Attributes)
            .WithOne(a => a.Sku)
            .HasForeignKey(a => a.SkuId)
            .OnDelete(DeleteBehavior.Cascade);
        
        map.Navigation(s => s.Attributes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}