using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.PaymentsPix.Domain.Entities;

namespace Vls.Shopflow.PaymentsPix.Infrastructure;

public sealed class PaymentsPixDbContext : DbContext
{
    public DbSet<PixPayment> PixPayments => Set<PixPayment>();

    public PaymentsPixDbContext(DbContextOptions<PaymentsPixDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("payments_pix");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsPixDbContext).Assembly);
    }
}
