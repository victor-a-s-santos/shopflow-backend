using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.QueryHandlers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.Orders.Infrastructure.Repositories;
using Vls.Shopflow.Orders.Infrastructure.Services;

namespace Vls.Shopflow.Orders.IntegrationTests;

public sealed class AdminDemandReportIntegrationTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static long _nextNumber = 8_000_000_000L + Environment.TickCount64;

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static OrdersDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "orders"))
            .Options;
        return new OrdersDbContext(options);
    }

    private static async Task<OrdersDbContext> OpenAsync()
    {
        var db = CreateContext();
        await db.Database.MigrateAsync();
        return db;
    }

    [Fact]
    public async Task ConfirmedDemand_CountsPaidSkuUnits_AndExcludesPendingCanceledExpired()
    {
        if (!await CanConnectAsync())
            return;

        var marker = Marker();
        var skuId = Guid.NewGuid();
        var otherSkuId = Guid.NewGuid();
        await using var db = await OpenAsync();

        await SavePaidAsync(db, marker, "Pago", skuId, "SKU-PAGO", 2, 10m, Package(2));
        await SaveAsync(db, Build(marker, "Pendente", [Item(skuId, marker, "SKU-PAGO", 9, 10m)]));
        var expired = Build(marker, "Expirado", [Item(skuId, marker, "SKU-PAGO", 7, 10m)]);
        expired.Expire();
        await SaveAsync(db, expired);
        var canceled = Build(marker, "Cancelado", [Item(skuId, marker, "SKU-PAGO", 8, 10m)]);
        Set(canceled, nameof(Order.Status), OrderStatus.Canceled);
        await SaveAsync(db, canceled);
        await SavePaidAsync(db, marker, "OutroSku", otherSkuId, "SKU-OUTRO", 5, 20m, null);

        var page = await SummaryAsync(db, marker);

        page.TotalItems.Should().Be(2);
        var paid = page.Items.Single(x => x.SkuId == skuId);
        paid.TotalQuantity.Should().Be(2);
        paid.OrderCount.Should().Be(1);
        paid.SubtotalTotal.Should().Be(20m);
        paid.SkuCode.Should().Be("SKU-PAGO");
        paid.ProductName.Should().Contain(marker);
        paid.SalesDisplay.Should().NotBeNull();
        paid.SalesDisplay!.PackageSize.Should().Be(3);
        paid.SalesDisplay.TotalPieces.Should().Be(6);
        page.Items.Single(x => x.SkuId == otherSkuId).TotalQuantity.Should().Be(5);
        page.Items.Select(x => x.TotalQuantity).Should().Equal(5, 2);
    }

    [Fact]
    public async Task ConfirmedDemand_SumsDistinctOrders_AndKeepsGuestBuyers()
    {
        if (!await CanConnectAsync())
            return;

        var marker = Marker();
        var skuId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await using var db = await OpenAsync();

        await SavePaidAsync(db, marker, "Guest", skuId, "SKU-G", 2, 10m, null, customerUserId: null, phone: "11988887777");
        await SavePaidAsync(db, marker, "Conta", skuId, "SKU-G", 3, 10m, null, customerUserId: customerId, phone: "");

        var summary = await SummaryAsync(db, marker);
        var group = summary.Items.Should().ContainSingle().Subject;
        group.TotalQuantity.Should().Be(5);
        group.OrderCount.Should().Be(2);

        var detail = await new GetAdminProductDemandOrdersQueryHandler(
                new AdminDemandReportReadModel(db),
                new NullAdminOrderPixPaymentReader())
            .Handle(new GetAdminProductDemandOrdersQuery(skuId, PageSize: 20), CancellationToken.None);

        detail.TotalItems.Should().Be(2);
        detail.Items.Sum(x => x.Quantity).Should().Be(5);
        detail.Items.Should().ContainSingle(x => x.IsGuest && x.PhoneDigits == "11988887777");
        detail.Items.Should().ContainSingle(x => !x.IsGuest && x.PhoneDigits == null && x.CustomerPhone == "");
        detail.Items.Should().OnlyContain(x => x.Status == nameof(OrderStatus.Paid));
    }

    [Fact]
    public async Task ConfirmedDemand_PeriodUsesPaymentConfirmation_AndPaginationIsStable()
    {
        if (!await CanConnectAsync())
            return;

        var marker = Marker();
        var skuOld = Guid.NewGuid();
        var skuMid = Guid.NewGuid();
        var skuNew = Guid.NewGuid();
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        await using var db = await OpenAsync();

        await SavePaidAsync(db, marker, "Antigo", skuOld, "SKU-OLD", 4, 10m, null, paidAt: today.AddDays(-2));
        await SavePaidAsync(db, marker, "Meio", skuMid, "SKU-MID", 3, 10m, null, paidAt: today.AddHours(2));
        await SavePaidAsync(db, marker, "Novo", skuNew, "SKU-NEW", 9, 10m, null, paidAt: today.AddHours(3));

        var filtered = await SummaryAsync(db, marker, from: today, pageSize: 1);
        filtered.TotalItems.Should().Be(2);
        filtered.TotalPages.Should().Be(2);
        filtered.Items.Should().ContainSingle();
        filtered.Items[0].SkuId.Should().Be(skuNew);
        filtered.Items[0].TotalQuantity.Should().Be(9);

        var page2 = await SummaryAsync(db, marker, from: today, page: 2, pageSize: 1);
        page2.Items.Should().ContainSingle();
        page2.Items[0].SkuId.Should().Be(skuMid);

        var first = await SummaryAsync(db, marker);
        var second = await SummaryAsync(db, marker);
        second.Items.Select(x => x.SkuId).Should().Equal(first.Items.Select(x => x.SkuId));
    }

    [Fact]
    public async Task ConfirmedDemand_OrdersSortByConfirmation_AndOrderCountSortsSeparately()
    {
        if (!await CanConnectAsync())
            return;

        var marker = Marker();
        var manyOrdersSku = Guid.NewGuid();
        var highQuantitySku = Guid.NewGuid();
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        await using var db = await OpenAsync();

        await SavePaidAsync(db, marker, "A", manyOrdersSku, "SKU-MANY", 1, 10m, null, paidAt: today.AddHours(1));
        await SavePaidAsync(db, marker, "B", manyOrdersSku, "SKU-MANY", 1, 10m, null, paidAt: today.AddHours(4));
        await SavePaidAsync(db, marker, "C", highQuantitySku, "SKU-QTY", 10, 10m, null, paidAt: today.AddHours(2));

        var byOrders = await SummaryAsync(db, marker, sort: AdminDemandReportSort.OrdersDesc);
        byOrders.Items[0].SkuId.Should().Be(manyOrdersSku);
        byOrders.Items[0].OrderCount.Should().Be(2);

        var detail = await new GetAdminProductDemandOrdersQueryHandler(
                new AdminDemandReportReadModel(db),
                new NullAdminOrderPixPaymentReader())
            .Handle(
                new GetAdminProductDemandOrdersQuery(manyOrdersSku, Sort: AdminDemandReportSort.PaidAtAsc),
                CancellationToken.None);

        detail.Items.Should().HaveCount(2);
        detail.Items[0].PaidAt.Should().BeBefore(detail.Items[1].PaidAt!.Value);
    }

    [Fact]
    public async Task UnpaidOrders_ReturnsActivePendingOnly_WithItemsContactSearchAndPagination()
    {
        if (!await CanConnectAsync())
            return;

        var marker = Marker();
        var skuId = Guid.NewGuid();
        var email = $"{marker}@shopflow.test";
        await using var db = await OpenAsync();

        var pending = Build(
            marker,
            "Ana",
            [
                Item(skuId, marker + " Camisa", "SKU-CAM", 1, 15m),
                Item(Guid.NewGuid(), marker + " Calca", "SKU-CAL", 2, 20m)
            ],
            email: email,
            phone: "(11) 97777-6666");
        await SaveAsync(db, pending);

        var noPhone = Build(marker, "SemTelefone", [Item(skuId, marker, "SKU-CAM", 1, 15m)], phone: "");
        await SaveAsync(db, noPhone);

        await SavePaidAsync(db, marker, "JaPago", skuId, "SKU-CAM", 4, 15m, null, email: $"pago-{marker}@shopflow.test");

        var expired = Build(marker, "Expirado", [Item(skuId, marker, "SKU-CAM", 1, 15m)]);
        expired.Expire();
        await SaveAsync(db, expired);

        var canceled = Build(marker, "Cancelado", [Item(skuId, marker, "SKU-CAM", 1, 15m)]);
        Set(canceled, nameof(Order.Status), OrderStatus.Canceled);
        await SaveAsync(db, canceled);

        var older = Build(marker, "Antigo", [Item(skuId, marker, "SKU-CAM", 1, 15m)]);
        Set(older, nameof(Order.CreatedAt), DateTimeOffset.UtcNow.AddDays(-10));
        await SaveAsync(db, older);

        var excluded = Build(marker, "PixPago", [Item(skuId, marker, "SKU-CAM", 1, 15m)]);
        await SaveAsync(db, excluded);

        var handler = new GetAdminUnpaidOrdersQueryHandler(
            new AdminDemandReportReadModel(db),
            new NullAdminOrderPixPaymentReader());

        var byEmail = await handler.Handle(new GetAdminUnpaidOrdersQuery(Q: email), CancellationToken.None);
        var match = byEmail.Items.Should().ContainSingle().Subject;
        match.OrderId.Should().Be(pending.Id);
        match.Status.Should().Be(nameof(OrderStatus.PendingPayment));
        match.IsGuest.Should().BeTrue();
        match.PhoneDigits.Should().Be("11977776666");
        match.Items.Should().HaveCount(2);
        match.Items.Select(x => x.SkuCode).Should().BeEquivalentTo("SKU-CAM", "SKU-CAL");
        match.Total.Should().Be(55m);

        var byProduct = await handler.Handle(new GetAdminUnpaidOrdersQuery(Q: marker + " Calca"), CancellationToken.None);
        byProduct.Items.Should().ContainSingle(x => x.OrderId == pending.Id);

        var bySku = await handler.Handle(new GetAdminUnpaidOrdersQuery(Q: marker), CancellationToken.None);
        bySku.Items.Should().Contain(x => x.OrderId == pending.Id);
        bySku.Items.Should().NotContain(x => x.OrderId == expired.Id);
        bySku.Items.Should().NotContain(x => x.OrderId == canceled.Id);
        bySku.Items.Should().NotContain(x => x.CustomerFullName == "JaPago");

        var emptyPhone = await handler.Handle(
            new GetAdminUnpaidOrdersQuery(Q: noPhone.CustomerEmail),
            CancellationToken.None);
        emptyPhone.Items.Should().ContainSingle();
        emptyPhone.Items[0].PhoneDigits.Should().BeNull();
        emptyPhone.Items[0].CustomerPhone.Should().BeEmpty();

        var fromToday = await handler.Handle(
            new GetAdminUnpaidOrdersQuery(Q: marker, From: DateTimeOffset.UtcNow.AddHours(-1)),
            CancellationToken.None);
        fromToday.Items.Should().NotContain(x => x.OrderId == older.Id);
        fromToday.Items.Should().Contain(x => x.OrderId == pending.Id);

        var readModel = new AdminDemandReportReadModel(db);
        var excludedPage = await readModel.GetUnpaidOrdersAsync(
            new AdminUnpaidOrdersQuerySpec(1, 20, null, null, marker, null, null, false, [pending.Id]),
            CancellationToken.None);
        excludedPage.Items.Should().NotContain(x => x.Id == pending.Id);

        var page = await handler.Handle(
            new GetAdminUnpaidOrdersQuery(Q: marker, Page: 1, PageSize: 1, Sort: AdminDemandReportSort.CreatedAtAsc),
            CancellationToken.None);
        page.PageSize.Should().Be(1);
        page.Items.Should().ContainSingle();
        page.TotalItems.Should().BeGreaterThan(1);
        page.TotalPages.Should().BeGreaterThan(1);
    }

    private static async Task<Vls.Shopflow.Orders.Application.DataTransferObjects.PagedAdminProductDemandDto> SummaryAsync(
        OrdersDbContext db,
        string marker,
        DateTimeOffset? from = null,
        int page = 1,
        int pageSize = 20,
        string? sort = null)
    {
        var handler = new GetAdminProductDemandQueryHandler(new AdminDemandReportReadModel(db));
        return await handler.Handle(
            new GetAdminProductDemandQuery(page, pageSize, from, null, marker, sort),
            CancellationToken.None);
    }

    private static async Task SavePaidAsync(
        OrdersDbContext db,
        string marker,
        string name,
        Guid skuId,
        string skuCode,
        int quantity,
        decimal unitPrice,
        OrderItemSalesSnapshot? snapshot,
        Guid? customerUserId = null,
        string? phone = "11999990000",
        string? email = null,
        DateTimeOffset? paidAt = null)
    {
        var order = Build(
            marker,
            name,
            [Item(skuId, marker + " " + name, skuCode, quantity, unitPrice, snapshot)],
            customerUserId,
            email,
            phone);
        order.MarkAsPaid(paidAt ?? DateTimeOffset.UtcNow);
        await SaveAsync(db, order);
    }

    private static async Task SaveAsync(OrdersDbContext db, Order order)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync();
    }

    private static Order Build(
        string marker,
        string name,
        IReadOnlyList<OrderItem> items,
        Guid? customerUserId = null,
        string? email = null,
        string? phone = "11999990000")
    {
        var subtotal = items.Sum(item => item.Subtotal);
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            name,
            email ?? $"{marker}-{Guid.NewGuid():N}@shopflow.test",
            phone ?? "",
            "01001000",
            "Rua A",
            "100",
            null,
            "Centro",
            "São Paulo",
            "SP",
            subtotal,
            null,
            subtotal,
            items,
            customerUserId);
        order.AssignOrderNumber(Interlocked.Increment(ref _nextNumber));
        return order;
    }

    private static OrderItem Item(
        Guid skuId,
        string productName,
        string skuCode,
        int quantity,
        decimal unitPrice,
        OrderItemSalesSnapshot? snapshot = null)
        => OrderItem.Create(skuId, productName, skuCode, quantity, unitPrice, snapshot);

    private static OrderItemSalesSnapshot Package(int quantity)
        => new(
            "FixedPackage",
            3,
            "Lote com 3 peças",
            null,
            "lote(s)",
            true,
            quantity * 3,
            null,
            $"{quantity} lote(s) = {quantity * 3} peças");

    private static string Marker() => "pd-" + Guid.NewGuid().ToString("N");

    private static void Set(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException(propertyName);
        property.SetValue(target, value);
    }
}
