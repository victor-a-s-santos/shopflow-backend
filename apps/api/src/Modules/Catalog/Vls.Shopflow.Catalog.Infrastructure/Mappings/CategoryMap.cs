using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Infrastructure.Mappings;

public class CategoryMap : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);

        builder.OwnsOne(x => x.Slug, s =>
        {
            s.Property(x => x.Value)
                .HasColumnName("slug")
                .HasMaxLength(200)
                .IsRequired();

            s.HasIndex(x => x.Value).IsUnique();
        });

        builder.Navigation(x => x.Slug).IsRequired();

        builder.HasMany(x => x.DefaultAttributes)
            .WithOne(a => a.Category)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
