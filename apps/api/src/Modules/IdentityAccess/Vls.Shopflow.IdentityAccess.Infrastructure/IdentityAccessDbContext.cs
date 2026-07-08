using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure;

public sealed class IdentityAccessDbContext : IdentityDbContext<ShopflowUser, ShopflowRole, Guid>
{
    public IdentityAccessDbContext(DbContextOptions<IdentityAccessDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");

        builder.Entity<ShopflowUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(u => u.FullName).HasMaxLength(256);
            entity.Property(u => u.IsStaff).IsRequired();
            entity.Property(u => u.IsActive).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
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
