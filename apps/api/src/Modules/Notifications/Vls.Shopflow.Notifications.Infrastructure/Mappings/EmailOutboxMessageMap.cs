using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.Notifications.Domain.Entities;

namespace Vls.Shopflow.Notifications.Infrastructure.Mappings;

internal sealed class EmailOutboxMessageMap : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> map)
    {
        map.ToTable("email_outbox");
        map.HasKey(x => x.Id);
        map.Property(x => x.Id).ValueGeneratedNever();

        map.Property(x => x.Type).HasConversion<string>().HasMaxLength(64).IsRequired();
        map.Property(x => x.RecipientEmail).HasMaxLength(EmailOutboxMessage.MaxRecipientEmailLength).IsRequired();
        map.Property(x => x.RecipientName).HasMaxLength(EmailOutboxMessage.MaxRecipientNameLength);
        map.Property(x => x.Subject).HasMaxLength(EmailOutboxMessage.MaxSubjectLength).IsRequired();
        map.Property(x => x.HtmlBody).IsRequired();
        map.Property(x => x.TextBody);
        map.Property(x => x.PayloadJson);
        map.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        map.Property(x => x.Attempts).IsRequired();
        map.Property(x => x.LastError).HasMaxLength(EmailOutboxMessage.MaxLastErrorLength);
        map.Property(x => x.ProviderMessageId).HasMaxLength(EmailOutboxMessage.MaxProviderMessageIdLength);
        map.Property(x => x.IdempotencyKey).HasMaxLength(EmailOutboxMessage.MaxIdempotencyKeyLength).IsRequired();
        map.Property(x => x.CreatedAt).IsRequired();
        map.Property(x => x.SentAt);
        map.Property(x => x.NextAttemptAt).IsRequired();

        map.HasIndex(x => x.IdempotencyKey).IsUnique();
        map.HasIndex(x => new { x.Status, x.NextAttemptAt });

        map.Ignore("_events");
        map.Ignore(x => x.DomainEvents);
    }
}
