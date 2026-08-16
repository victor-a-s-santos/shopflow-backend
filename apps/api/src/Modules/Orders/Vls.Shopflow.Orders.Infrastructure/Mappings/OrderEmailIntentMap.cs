using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Infrastructure.Mappings;

internal sealed class OrderEmailIntentMap : IEntityTypeConfiguration<OrderEmailIntent>
{
    public void Configure(EntityTypeBuilder<OrderEmailIntent> map)
    {
        map.ToTable("email_intents");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.OrderId).IsRequired();
        map.Property(x => x.Type).HasConversion<string>().HasMaxLength(OrderEmailIntent.MaxTypeLength).IsRequired();
        map.Property(x => x.IdempotencyKey).HasMaxLength(OrderEmailIntent.MaxIdempotencyKeyLength).IsRequired();
        map.Property(x => x.PayloadJson).IsRequired();
        map.Property(x => x.Status).HasConversion<string>().HasMaxLength(OrderEmailIntent.MaxStatusLength).IsRequired();
        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.DispatchedAt);

        map.HasIndex(x => x.IdempotencyKey).IsUnique();
        map.HasIndex(x => new { x.Status, x.CreatedAt });
        map.HasIndex(x => new { x.OrderId, x.Type });

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}
