using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.Orders.Infrastructure.Repositories;
using Vls.Shopflow.PaymentsPix.Infrastructure.Services;

namespace Vls.Shopflow.PaymentsPix.IntegrationTests;

public sealed class OrderPaidEmailIntentIntegrationTests
{
    [Fact]
    public async Task MarkAsPaid_CreatesSinglePaymentConfirmedIntent_AndAlreadyPaidRepairsIdempotently()
    {
        if (!await CanConnectAsync())
            return;

        var orderId = await CreatePixPaymentIntegrationTests.SeedPendingOrderAsync();

        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();
        var writer = new OrderPaidWriter(ordersDb, new OrderEmailIntentRepository(ordersDb));

        var first = await writer.MarkAsPaidAsync(orderId, DateTimeOffset.UtcNow, CancellationToken.None);
        first.Found.Should().BeTrue();
        first.MarkedPaid.Should().BeTrue();

        var second = await writer.MarkAsPaidAsync(orderId, DateTimeOffset.UtcNow, CancellationToken.None);
        second.AlreadyPaid.Should().BeTrue();
        second.MarkedPaid.Should().BeFalse();

        var intents = await ordersDb.EmailIntents
            .AsNoTracking()
            .Where(i => i.OrderId == orderId && i.Type == OrderEmailIntentType.PaymentConfirmed)
            .ToListAsync();

        intents.Should().ContainSingle();
        intents[0].IdempotencyKey.Should().Be($"order:{orderId:D}:paid");
        intents[0].Status.Should().Be(OrderEmailIntentStatus.Pending);
    }

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateOrdersContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static OrdersDbContext CreateOrdersContext()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
            ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "orders"))
            .Options;
        return new OrdersDbContext(options);
    }
}
