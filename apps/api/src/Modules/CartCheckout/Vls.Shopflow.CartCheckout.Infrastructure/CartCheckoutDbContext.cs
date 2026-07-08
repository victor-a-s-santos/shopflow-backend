using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.CartCheckout.Domain.Entities;

namespace Vls.Shopflow.CartCheckout.Infrastructure;

public sealed class CartCheckoutDbContext : DbContext
{
    public DbSet<CheckoutSession> CheckoutSessions => Set<CheckoutSession>();
    public DbSet<CheckoutSessionItem> CheckoutSessionItems => Set<CheckoutSessionItem>();

    public CartCheckoutDbContext(DbContextOptions<CartCheckoutDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("cartcheckout");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartCheckoutDbContext).Assembly);
    }
}
