using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Infrastructure.Mappings;

public class AttributeValueDefinitionMap: IEntityTypeConfiguration<AttributeValueDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeValueDefinition> builder)
    {
        builder.ToTable("attribute_value_definitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.HexColor).HasMaxLength(7);
    }
}