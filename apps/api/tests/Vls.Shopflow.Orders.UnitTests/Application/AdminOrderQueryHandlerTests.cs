using System.Text.Json;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.QueryHandlers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Validators;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class AdminOrderQueryHandlerTests
{
    [Fact]
    public async Task GetAdminOrders_ReturnsPagedSortedByCreatedAtDesc()
    {
        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var readModel = new Mock<IAdminOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<AdminOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminOrderListPage(
            [
                Row(newerId, "Maria", "maria@test.com", "11911111111", DateTimeOffset.UtcNow),
                Row(olderId, "João", "joao@test.com", "11922222222", DateTimeOffset.UtcNow.AddDays(-1))
            ], 2));

        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        var sut = new GetAdminOrdersQueryHandler(readModel.Object, paymentReader.Object);
        var result = await sut.Handle(new GetAdminOrdersQuery(1, 20), CancellationToken.None);

        result.TotalItems.Should().Be(2);
        result.Items[0].Id.Should().Be(newerId);
        result.Items[1].Id.Should().Be(olderId);
        readModel.Verify(x => x.GetPagedAsync(
            It.Is<AdminOrderListQuerySpec>(s => s.SortCreatedAtAscending == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAdminOrders_FiltersByStatus()
    {
        var readModel = new Mock<IAdminOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<AdminOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminOrderListPage([], 0));
        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        var sut = new GetAdminOrdersQueryHandler(readModel.Object, paymentReader.Object);
        await sut.Handle(new GetAdminOrdersQuery(Status: "Paid"), CancellationToken.None);

        readModel.Verify(x => x.GetPagedAsync(
            It.Is<AdminOrderListQuerySpec>(s => s.Status == OrderStatus.Paid),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAdminOrders_FiltersByPaymentStatus()
    {
        var orderId = Guid.NewGuid();
        var readModel = new Mock<IAdminOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<AdminOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminOrderListPage([Row(orderId, "A", "a@b.com", "1", DateTimeOffset.UtcNow)], 1));

        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.FindOrderIdsByLatestPaymentStatusAsync("Paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync([orderId]);
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>
            {
                [orderId] = SamplePayment(orderId)
            });

        var sut = new GetAdminOrdersQueryHandler(readModel.Object, paymentReader.Object);
        var result = await sut.Handle(new GetAdminOrdersQuery(PaymentStatus: "Paid"), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Payment!.Status.Should().Be("Paid");
        readModel.Verify(x => x.GetPagedAsync(
            It.Is<AdminOrderListQuerySpec>(s => s.RestrictToOrderIds!.Single() == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("maria@test.com", null)]
    [InlineData("Maria", null)]
    [InlineData("11988887777", null)]
    public async Task GetAdminOrders_SearchesByText(string q, Guid? _)
    {
        var readModel = new Mock<IAdminOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<AdminOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminOrderListPage([], 0));
        var paymentReader = MockEmptyPayments();

        var sut = new GetAdminOrdersQueryHandler(readModel.Object, paymentReader.Object);
        await sut.Handle(new GetAdminOrdersQuery(Q: q), CancellationToken.None);

        readModel.Verify(x => x.GetPagedAsync(
            It.Is<AdminOrderListQuerySpec>(s => s.SearchText == q && s.SearchOrderId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAdminOrders_SearchesByOrderIdWhenQIsGuid()
    {
        var orderId = Guid.NewGuid();
        var readModel = new Mock<IAdminOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<AdminOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminOrderListPage([], 0));
        var paymentReader = MockEmptyPayments();

        var sut = new GetAdminOrdersQueryHandler(readModel.Object, paymentReader.Object);
        await sut.Handle(new GetAdminOrdersQuery(Q: orderId.ToString("D")), CancellationToken.None);

        readModel.Verify(x => x.GetPagedAsync(
            It.Is<AdminOrderListQuerySpec>(s => s.SearchOrderId == orderId && s.SearchText == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAdminOrders_IncludesPaymentSummaryWhenPresent()
    {
        var orderId = Guid.NewGuid();
        var readModel = new Mock<IAdminOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<AdminOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminOrderListPage([Row(orderId, "A", "a@b.com", "1", DateTimeOffset.UtcNow)], 1));

        var payment = SamplePayment(orderId);
        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto> { [orderId] = payment });

        var sut = new GetAdminOrdersQueryHandler(readModel.Object, paymentReader.Object);
        var result = await sut.Handle(new GetAdminOrdersQuery(), CancellationToken.None);

        result.Items[0].Payment.Should().BeEquivalentTo(payment);
    }

    [Fact]
    public async Task GetAdminOrders_ReturnsNullPaymentWhenMissing()
    {
        var orderId = Guid.NewGuid();
        var readModel = new Mock<IAdminOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<AdminOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminOrderListPage([Row(orderId, "A", "a@b.com", "1", DateTimeOffset.UtcNow)], 1));

        var sut = new GetAdminOrdersQueryHandler(readModel.Object, MockEmptyPayments().Object);
        var result = await sut.Handle(new GetAdminOrdersQuery(), CancellationToken.None);

        result.Items[0].Payment.Should().BeNull();
    }

    [Fact]
    public async Task GetAdminOrderById_ReturnsDetailWithCustomerShippingItemsPayment()
    {
        var order = CreateOrder("Ana Souza", "ana@test.com", "11999990000");
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var payment = SamplePayment(order.Id);
        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var sut = new GetAdminOrderByIdQueryHandler(repo.Object, paymentReader.Object);
        var result = await sut.Handle(new GetAdminOrderByIdQuery(order.Id), CancellationToken.None);

        result.Id.Should().Be(order.Id);
        result.Customer.Email.Should().Be("ana@test.com");
        result.ShippingAddress.City.Should().Be("São Paulo");
        result.Amounts.Total.Should().Be(100m);
        result.Items.Should().ContainSingle();
        result.Payment.Should().BeEquivalentTo(payment);
    }

    [Fact]
    public async Task GetAdminOrderById_WhenMissing_ThrowsNotFound()
    {
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var sut = new GetAdminOrderByIdQueryHandler(repo.Object, Mock.Of<IAdminOrderPixPaymentReader>());
        var act = () => sut.Handle(new GetAdminOrderByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    [Fact]
    public void AdminOrderDtos_DoNotExposeSensitivePaymentFields()
    {
        var payment = SamplePayment(Guid.NewGuid());
        var json = JsonSerializer.Serialize(payment);

        json.Should().NotContain("CopyPaste");
        json.Should().NotContain("QrCode");
        json.Should().NotContain("TicketUrl");
        json.Should().NotContain("GuestAccess");
        json.Should().NotContain("AccessToken");
        json.Should().NotContain("WebhookSecret");
        json.Should().Match(s =>
            s.Contains("providerOrderId", StringComparison.OrdinalIgnoreCase)
            || s.Contains("ProviderOrderId", StringComparison.Ordinal));
        json.Should().NotContain("000201"); // Pix EMV payload would only appear in copy-paste
    }

    [Fact]
    public void GetAdminOrdersQueryValidator_RejectsPageSizeAbove100()
    {
        var validator = new GetAdminOrdersQueryValidator();
        var result = validator.TestValidate(new GetAdminOrdersQuery(Page: 1, PageSize: 101));
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void GetAdminOrdersQueryValidator_RejectsInvalidStatus()
    {
        var validator = new GetAdminOrdersQueryValidator();
        var result = validator.TestValidate(new GetAdminOrdersQuery(Status: "Nope"));
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void GetAdminOrdersQueryValidator_RejectsInvalidDateRange()
    {
        var validator = new GetAdminOrdersQueryValidator();
        var result = validator.TestValidate(new GetAdminOrdersQuery(
            CreatedFrom: DateTimeOffset.UtcNow,
            CreatedTo: DateTimeOffset.UtcNow.AddDays(-1)));
        result.ShouldHaveAnyValidationError();
    }

    private static Mock<IAdminOrderPixPaymentReader> MockEmptyPayments()
    {
        var mock = new Mock<IAdminOrderPixPaymentReader>();
        mock.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());
        return mock;
    }

    private static AdminOrderListRow Row(
        Guid id,
        string name,
        string email,
        string phone,
        DateTimeOffset createdAt)
        => new(id, OrderStatus.PendingPayment, name, email, phone, 100m, null, 100m, createdAt, null, 1);

    private static AdminOrderPaymentSummaryDto SamplePayment(Guid orderId)
        => new(
            Guid.NewGuid(),
            "MercadoPago",
            "Paid",
            "ORD01TEST",
            "PAY01TEST",
            "PAY01TEST",
            "processed",
            "accredited",
            "processed",
            "accredited",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(30));

    private static Order CreateOrder(string name, string email, string phone)
    {
        var item = OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 100m);
        return Order.CreatePendingPayment(
            Guid.NewGuid(),
            name,
            email,
            phone,
            "01001000",
            "Rua A",
            "100",
            null,
            "Centro",
            "São Paulo",
            "SP",
            100m,
            null,
            100m,
            [item]);
    }
}
