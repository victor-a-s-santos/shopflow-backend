using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Infrastructure;

public sealed class OrdersDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<GuestOrderAccessToken> GuestOrderAccessTokens => Set<GuestOrderAccessToken>();
    public DbSet<DeliveryBatch> DeliveryBatches => Set<DeliveryBatch>();
    public DbSet<DeliveryBatchOrder> DeliveryBatchOrders => Set<DeliveryBatchOrder>();
    public DbSet<OrderEmailIntent> EmailIntents => Set<OrderEmailIntent>();

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("orders");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }
}
