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

public sealed class CustomerOrderQueryHandlerTests
{
    [Fact]
    public async Task GetCustomerOrders_ReturnsOnlyScopedCustomerUserId()
    {
        var customerId = Guid.NewGuid();
        var readModel = new Mock<ICustomerOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<CustomerOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerOrderListPage(
            [
                new CustomerOrderListRow(
                    Guid.NewGuid(), OrderStatus.Paid, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    100m, null, 100m, 1, "Camiseta")
            ], 1));

        var paymentReader = new Mock<ICustomerOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, CustomerOrderPaymentSummaryDto>());

        var sut = new GetCustomerOrdersQueryHandler(readModel.Object, paymentReader.Object);
        await sut.Handle(new GetCustomerOrdersQuery(customerId), CancellationToken.None);

        readModel.Verify(x => x.GetPagedAsync(
            It.Is<CustomerOrderListQuerySpec>(s => s.CustomerUserId == customerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCustomerOrders_FiltersByStatusAndPaymentStatus()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var readModel = new Mock<ICustomerOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<CustomerOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerOrderListPage([], 0));

        var paymentReader = new Mock<ICustomerOrderPixPaymentReader>();
        paymentReader.Setup(x => x.FindOrderIdsByLatestPaymentStatusAsync("Pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync([orderId]);
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, CustomerOrderPaymentSummaryDto>());

        var sut = new GetCustomerOrdersQueryHandler(readModel.Object, paymentReader.Object);
        await sut.Handle(new GetCustomerOrdersQuery(customerId, Status: "Paid", PaymentStatus: "Pending"), CancellationToken.None);

        readModel.Verify(x => x.GetPagedAsync(
            It.Is<CustomerOrderListQuerySpec>(s =>
                s.Status == OrderStatus.Paid
                && s.RestrictToOrderIds!.Single() == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCustomerOrders_IncludesPaymentSummary()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var readModel = new Mock<ICustomerOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<CustomerOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerOrderListPage(
            [
                new CustomerOrderListRow(
                    orderId, OrderStatus.PendingPayment, DateTimeOffset.UtcNow, null,
                    50m, null, 50m, 1, "Item")
            ], 1));

        var payment = new CustomerOrderPaymentSummaryDto("Paid", "MercadoPago", DateTimeOffset.UtcNow, null);
        var paymentReader = new Mock<ICustomerOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, CustomerOrderPaymentSummaryDto> { [orderId] = payment });

        var sut = new GetCustomerOrdersQueryHandler(readModel.Object, paymentReader.Object);
        var result = await sut.Handle(new GetCustomerOrdersQuery(customerId), CancellationToken.None);

        result.Items[0].Payment!.Status.Should().Be("Paid");
        result.Items[0].Payment.Provider.Should().Be("MercadoPago");
    }

    [Fact]
    public async Task GetCustomerOrderById_OwnOrder_ReturnsDetail()
    {
        var customerId = Guid.NewGuid();
        var order = CreateBoundOrder(customerId);
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var payment = new CustomerOrderPaymentSummaryDto("Pending", "MercadoPago", null, DateTimeOffset.UtcNow.AddMinutes(30));
        var paymentReader = new Mock<ICustomerOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var sut = new GetCustomerOrderByIdQueryHandler(repo.Object, paymentReader.Object);
        var result = await sut.Handle(new GetCustomerOrderByIdQuery(customerId, order.Id), CancellationToken.None);

        result.Id.Should().Be(order.Id);
        result.Payment!.Status.Should().Be("Pending");
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetCustomerOrderById_OtherCustomer_ThrowsNotFound()
    {
        var order = CreateBoundOrder(Guid.NewGuid());
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var sut = new GetCustomerOrderByIdQueryHandler(repo.Object, Mock.Of<ICustomerOrderPixPaymentReader>());
        var act = () => sut.Handle(new GetCustomerOrderByIdQuery(Guid.NewGuid(), order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    [Fact]
    public async Task GetCustomerOrderById_GuestOrder_ThrowsNotFound()
    {
        var order = CreateBoundOrder(customerUserId: null);
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var sut = new GetCustomerOrderByIdQueryHandler(repo.Object, Mock.Of<ICustomerOrderPixPaymentReader>());
        var act = () => sut.Handle(new GetCustomerOrderByIdQuery(Guid.NewGuid(), order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    [Fact]
    public void CustomerPaymentDto_DoesNotSerializeProviderIdsOrQr()
    {
        var dto = new CustomerOrderPaymentSummaryDto("Paid", "MercadoPago", DateTimeOffset.UtcNow, null);
        var json = JsonSerializer.Serialize(dto);

        json.Should().NotContain("ProviderOrder");
        json.Should().NotContain("CopyPaste");
        json.Should().NotContain("QrCode");
        json.Should().NotContain("TicketUrl");
        json.Should().NotContain("AccessToken");
        json.Should().NotContain("WebhookSecret");
        json.Should().Contain("Paid");
    }

    [Fact]
    public void GetCustomerOrdersQueryValidator_RejectsPageSizeAbove50()
    {
        var validator = new GetCustomerOrdersQueryValidator();
        var result = validator.TestValidate(new GetCustomerOrdersQuery(Guid.NewGuid(), PageSize: 51));
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    private static Order CreateBoundOrder(Guid? customerUserId)
    {
        var item = OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 40m);
        return Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Cliente",
            "c@test.com",
            "11999999999",
            "01001000",
            "Rua",
            "1",
            null,
            "Bairro",
            "São Paulo",
            "SP",
            40m,
            null,
            40m,
            [item],
            customerUserId);
    }
}
