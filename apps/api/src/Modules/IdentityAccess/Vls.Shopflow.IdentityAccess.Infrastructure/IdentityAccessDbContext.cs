using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.IdentityAccess.Domain.Entities;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure;

public sealed class IdentityAccessDbContext : IdentityDbContext<ShopflowUser, ShopflowRole, Guid>
{
    public IdentityAccessDbContext(DbContextOptions<IdentityAccessDbContext> options)
        : base(options)
    {
    }

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");

        builder.Entity<CustomerAddress>(entity =>
        {
            entity.ToTable("customer_addresses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.CustomerUserId).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(40);
            entity.Property(x => x.RecipientName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PostalCode).HasMaxLength(8).IsRequired();
            entity.Property(x => x.Street).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Number).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Complement).HasMaxLength(120);
            entity.Property(x => x.Neighborhood).HasMaxLength(120).IsRequired();
            entity.Property(x => x.City).HasMaxLength(120).IsRequired();
            entity.Property(x => x.State).HasMaxLength(2).IsRequired();
            entity.Property(x => x.Country).HasMaxLength(2).IsRequired();
            entity.Property(x => x.IsDefault).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasIndex(x => x.CustomerUserId);
            entity.HasIndex(x => new { x.CustomerUserId, x.IsDefault });
        });

        builder.Entity<ShopflowUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(u => u.FullName).HasMaxLength(256);
            entity.Property(u => u.IsStaff).IsRequired();
            entity.Property(u => u.IsActive).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.AccessStatus).IsRequired();
            entity.Property(u => u.AccessDecisionReason).HasMaxLength(1000);
            entity.HasIndex(u => new { u.IsStaff, u.AccessStatus, u.AccessRequestedAt });
        });

        builder.Entity<ShopflowRole>(entity =>
        {
            entity.ToTable("roles");
        });

        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
