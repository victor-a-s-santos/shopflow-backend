using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using System.Text.Json;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.QueryHandlers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Services;
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
                    Guid.NewGuid(), 10582, OrderStatus.Paid, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    100m, null, 100m, 1, "Camiseta",
                    FulfillmentStatus.AwaitingShipment, null, null, null, null)
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
    public async Task GetCustomerOrders_FiltersByPublicCustomerStatusAndPaymentStatus()
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
        await sut.Handle(
            new GetCustomerOrdersQuery(customerId, Status: "Confirmed", PaymentStatus: "Pending"),
            CancellationToken.None);

        readModel.Verify(x => x.GetPagedAsync(
            It.Is<CustomerOrderListQuerySpec>(s =>
                s.Status == OrderStatus.Paid
                && s.RestrictToOrderIds!.Single() == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCustomerOrders_ProjectsCustomerStatusAndPixMethod()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var readModel = new Mock<ICustomerOrderReadModel>();
        readModel.Setup(x => x.GetPagedAsync(It.IsAny<CustomerOrderListQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerOrderListPage(
            [
                new CustomerOrderListRow(
                    orderId, 10583, OrderStatus.PendingPayment, DateTimeOffset.UtcNow, null,
                    50m, null, 50m, 1, "Item",
                    FulfillmentStatus.AwaitingShipment, null, null, null, null)
            ], 1));

        var payment = new CustomerOrderPaymentSummaryDto(
            "Paid",
            OrderCustomerStatusProjector.PaymentMethodPix,
            DateTimeOffset.UtcNow,
            null);
        var paymentReader = new Mock<ICustomerOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, CustomerOrderPaymentSummaryDto> { [orderId] = payment });

        var sut = new GetCustomerOrdersQueryHandler(readModel.Object, paymentReader.Object);
        var result = await sut.Handle(new GetCustomerOrdersQuery(customerId), CancellationToken.None);

        result.Items[0].CustomerStatus.Should().Be(OrderCustomerStatusCodes.Confirmed);
        result.Items[0].Payment!.Status.Should().Be("Paid");
        result.Items[0].Payment.Method.Should().Be("Pix");
        result.Items[0].Payment.ExpiresAt.Should().BeNull();
        result.Items[0].Currency.Should().Be("BRL");
        result.Items[0].OrderNumber.Should().Be("10583");
        result.Items[0].FulfillmentStatus.Should().Be("AwaitingShipment");
    }

    [Fact]
    public async Task GetCustomerOrderById_OwnOrder_ExposesDeliveryWithoutInternalNote()
    {
        var customerId = Guid.NewGuid();
        var order = CreateBoundOrder(customerId);
        order.SetDeliveryPreference(DeliveryMethod.Carrier, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), "Junto com pedido anterior");
        order.SetInternalOrderNote("Não mostrar ao cliente");
        order.MarkAsPaid();
        order.MarkAsShipped(Guid.NewGuid(), DeliveryMethod.Carrier, "ABC123");

        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var paymentReader = new Mock<ICustomerOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerOrderPaymentSummaryDto?)null);

        var sut = new GetCustomerOrderByIdQueryHandler(repo.Object, paymentReader.Object);
        var result = await sut.Handle(new GetCustomerOrderByIdQuery(customerId, order.Id), CancellationToken.None);

        result.Delivery.Should().NotBeNull();
        result.Delivery!.FulfillmentStatus.Should().Be("Shipped");
        result.Delivery.CustomerOrderNote.Should().Be("Junto com pedido anterior");
        result.Delivery.TrackingCode.Should().Be("ABC123");

        var json = JsonSerializer.Serialize(result);
        json.Should().NotContain("InternalOrderNote");
        json.Should().NotContain("Não mostrar ao cliente");
        json.Should().NotContain("FulfillmentUpdatedByAdminId");
    }

    [Fact]
    public async Task GetCustomerOrderById_OwnOrder_ReturnsDetail()
    {
        var customerId = Guid.NewGuid();
        var order = CreateBoundOrder(customerId);
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        var payment = new CustomerOrderPaymentSummaryDto(
            "Pending",
            OrderCustomerStatusProjector.PaymentMethodPix,
            null,
            expiresAt);
        var paymentReader = new Mock<ICustomerOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var sut = new GetCustomerOrderByIdQueryHandler(repo.Object, paymentReader.Object);
        var result = await sut.Handle(new GetCustomerOrderByIdQuery(customerId, order.Id), CancellationToken.None);

        result.Id.Should().Be(order.Id);
        result.CustomerStatus.Should().Be(OrderCustomerStatusCodes.AwaitingPayment);
        result.Payment!.Status.Should().Be("Pending");
        result.Payment.ExpiresAt.Should().Be(expiresAt);
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
    public void CustomerPaymentDto_DoesNotSerializeProviderOrSecrets()
    {
        var dto = new CustomerOrderPaymentSummaryDto(
            "Paid",
            OrderCustomerStatusProjector.PaymentMethodPix,
            DateTimeOffset.UtcNow,
            null);
        var json = JsonSerializer.Serialize(dto);

        json.Should().NotContain("Provider");
        json.Should().NotContain("MercadoPago");
        json.Should().NotContain("ProviderOrder");
        json.Should().NotContain("CopyPaste");
        json.Should().NotContain("QrCode");
        json.Should().NotContain("TicketUrl");
        json.Should().NotContain("AccessToken");
        json.Should().NotContain("WebhookSecret");
        json.Should().Contain("Paid");
        json.Should().Contain("Pix");
    }

    [Fact]
    public void GetCustomerOrdersQueryValidator_RejectsPageSizeAbove50()
    {
        var validator = new GetCustomerOrdersQueryValidator();
        var result = validator.TestValidate(new GetCustomerOrdersQuery(Guid.NewGuid(), PageSize: 51));
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void GetCustomerOrdersQueryValidator_AcceptsPublicStatusCodes()
    {
        var validator = new GetCustomerOrdersQueryValidator();
        validator.TestValidate(new GetCustomerOrdersQuery(Guid.NewGuid(), Status: "AwaitingPayment"))
            .ShouldNotHaveValidationErrorFor(x => x.Status);
        validator.TestValidate(new GetCustomerOrdersQuery(Guid.NewGuid(), Status: "Confirmed"))
            .ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void GetCustomerOrdersQueryValidator_RejectsInvalidDateRange()
    {
        var validator = new GetCustomerOrdersQueryValidator();
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(-1);
        var result = validator.TestValidate(
            new GetCustomerOrdersQuery(Guid.NewGuid(), CreatedFrom: from, CreatedTo: to));
        result.ShouldHaveValidationErrorFor(x => x);
    }

    private static Order CreateBoundOrder(Guid? customerUserId)
    {
        var item = OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 40m);
        var order = Order.CreatePendingPayment(
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
        order.AssignOrderNumber(10590);
        return order;
    }
}
