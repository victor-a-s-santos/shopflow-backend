using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Infrastructure.Mappings;

public class SkuAttributeMap : IEntityTypeConfiguration<SkuAttribute>
{
    public void Configure(EntityTypeBuilder<SkuAttribute> builder)
    {
        builder.ToTable("sku_attributes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SkuId).IsRequired();

        builder.HasOne(x => x.AttributeDefinition)
            .WithMany()
            .HasForeignKey(x => x.AttributeDefinitionId);

        builder.HasOne(x => x.AttributeValueDefinition)
            .WithMany()
            .HasForeignKey(x => x.AttributeValueDefinitionId);

        builder.Property(x => x.CustomName).HasMaxLength(150);
        builder.Property(x => x.CustomValue).HasMaxLength(150);
    }
}