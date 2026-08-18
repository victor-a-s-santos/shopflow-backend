using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Infrastructure.Mappings;

internal sealed class GuestOrderAccessTokenMap : IEntityTypeConfiguration<GuestOrderAccessToken>
{
    public void Configure(EntityTypeBuilder<GuestOrderAccessToken> map)
    {
        map.ToTable("guest_order_access_tokens");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.OrderId).IsRequired();
        map.HasIndex(x => x.OrderId);

        map.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        map.HasIndex(x => x.TokenHash);

        map.HasIndex(x => new { x.OrderId, x.TokenHash }).IsUnique();

        map.Property(x => x.TokenHashAlgorithm).HasMaxLength(30).IsRequired();
        map.Property(x => x.Purpose).HasMaxLength(50).IsRequired();
        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.ExpiresAt).IsRequired();
        map.HasIndex(x => x.ExpiresAt);
        map.Property(x => x.RevokedAt);
        map.Property(x => x.LastUsedAt);
        map.Property(x => x.UsageCount).IsRequired();

        map.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
