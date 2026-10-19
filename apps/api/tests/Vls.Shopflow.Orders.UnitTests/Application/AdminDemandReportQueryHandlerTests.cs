using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.QueryHandlers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Services;
using Vls.Shopflow.Orders.Application.Validators;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class AdminDemandReportQueryHandlerTests
{
    [Fact]
    public void PhoneDigits_UsesStoredDigits_AndDoesNotInventCountryCode()
    {
        AdminReportContact.PhoneDigits("(11) 98888-7777").Should().Be("11988887777");
        AdminReportContact.PhoneDigits("+55 11 98888-7777").Should().Be("5511988887777");
        AdminReportContact.PhoneDigits("").Should().BeNull();
        AdminReportContact.PhoneDigits("   ").Should().BeNull();
        AdminReportContact.PhoneDigits("abc").Should().BeNull();
        AdminReportContact.PhoneDigits("12345").Should().BeNull();
        AdminReportContact.PhoneDigits(null).Should().BeNull();
        AdminReportContact.PhoneDigits("1234567890123456").Should().BeNull();
    }

    [Fact]
    public void Validators_RejectInvalidPageSortAndPeriod()
    {
        var demand = new GetAdminProductDemandQueryValidator();
        demand.TestValidate(new GetAdminProductDemandQuery(Page: 0)).ShouldHaveValidationErrorFor(x => x.Page);
        demand.TestValidate(new GetAdminProductDemandQuery(PageSize: 101)).ShouldHaveValidationErrorFor(x => x.PageSize);
        demand.TestValidate(new GetAdminProductDemandQuery(Sort: "name")).ShouldHaveValidationErrorFor(x => x.Sort);
        demand.TestValidate(new GetAdminProductDemandQuery(
            From: DateTimeOffset.UtcNow,
            To: DateTimeOffset.UtcNow.AddDays(-1))).ShouldHaveAnyValidationError();

        var orders = new GetAdminProductDemandOrdersQueryValidator();
        orders.TestValidate(new GetAdminProductDemandOrdersQuery(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.SkuId);

        var unpaid = new GetAdminUnpaidOrdersQueryValidator();
        unpaid.TestValidate(new GetAdminUnpaidOrdersQuery(Sort: "quantity_desc")).ShouldHaveValidationErrorFor(x => x.Sort);
    }

    [Fact]
    public void UnpaidSearch_TreatsPhoneLengthAsText_AndGuidAsId()
    {
        var phone = GetAdminUnpaidOrdersQueryHandler.ParseSearch("11988887777");
        phone.SearchText.Should().Be("11988887777");
        phone.OrderNumber.Should().BeNull();

        var number = GetAdminUnpaidOrdersQueryHandler.ParseSearch("#10582");
        number.OrderNumber.Should().Be(10582);
        number.SearchText.Should().BeNull();

        var id = Guid.NewGuid();
        var guid = GetAdminUnpaidOrdersQueryHandler.ParseSearch(id.ToString());
        guid.OrderOrSkuId.Should().Be(id);
    }

    [Fact]
    public async Task UnpaidHandler_ExcludesOrdersWhoseLatestPixIsPaid()
    {
        var paidOrderId = Guid.NewGuid();
        var readModel = new Mock<IAdminDemandReportReadModel>();
        readModel.Setup(x => x.GetUnpaidOrdersAsync(It.IsAny<AdminUnpaidOrdersQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUnpaidOrdersPage([], 0));

        var payments = new Mock<IAdminOrderPixPaymentReader>();
        payments.Setup(x => x.FindOrderIdsByLatestPaymentStatusAsync("Paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { paidOrderId });
        payments.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        var sut = new GetAdminUnpaidOrdersQueryHandler(readModel.Object, payments.Object);
        await sut.Handle(new GetAdminUnpaidOrdersQuery(), CancellationToken.None);

        readModel.Verify(x => x.GetUnpaidOrdersAsync(
            It.Is<AdminUnpaidOrdersQuerySpec>(spec => spec.ExcludeOrderIds!.Single() == paidOrderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnpaidHandler_ReturnsContactItemsAndNullPhoneDigits()
    {
        var orderId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var readModel = new Mock<IAdminDemandReportReadModel>();
        readModel.Setup(x => x.GetUnpaidOrdersAsync(It.IsAny<AdminUnpaidOrdersQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUnpaidOrdersPage(
            [
                new AdminUnpaidOrderRow(
                    orderId,
                    44001,
                    DateTimeOffset.UtcNow,
                    OrderStatus.PendingPayment,
                    null,
                    "Convidado",
                    "guest@shopflow.test",
                    "",
                    30m,
                    null,
                    30m,
                    [
                        new AdminUnpaidOrderItemRow(Guid.NewGuid(), skuId, "SKU-A", "Camisa", 1, 10m, 10m, null, null, null, null, null, null, null, null, null),
                        new AdminUnpaidOrderItemRow(Guid.NewGuid(), Guid.NewGuid(), "SKU-B", "Calca", 2, 10m, 20m, null, null, null, null, null, null, null, null, null)
                    ])
            ], 1));

        var payment = new AdminOrderPaymentSummaryDto(
            Guid.NewGuid(), "MercadoPago", "Pending", null, null, null, null, null, null, null, null, null, null, null);
        var payments = new Mock<IAdminOrderPixPaymentReader>();
        payments.Setup(x => x.FindOrderIdsByLatestPaymentStatusAsync("Paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        payments.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto> { [orderId] = payment });

        var sut = new GetAdminUnpaidOrdersQueryHandler(readModel.Object, payments.Object);
        var result = await sut.Handle(new GetAdminUnpaidOrdersQuery(), CancellationToken.None);

        var order = result.Items.Should().ContainSingle().Subject;
        order.IsGuest.Should().BeTrue();
        order.CustomerPhone.Should().BeEmpty();
        order.PhoneDigits.Should().BeNull();
        order.Items.Should().HaveCount(2);
        order.Payment!.Status.Should().Be("Pending");
        order.OrderNumber.Should().Be("44001");
    }

    [Fact]
    public async Task DemandOrdersHandler_AttachesPaymentAndNormalizesPhone()
    {
        var orderId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var readModel = new Mock<IAdminDemandReportReadModel>();
        readModel.Setup(x => x.GetProductDemandOrdersAsync(It.IsAny<AdminDemandOrdersQuerySpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminDemandOrdersPage(
            [
                new AdminDemandOrderRow(
                    orderId, 44002, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, OrderStatus.Paid, Guid.NewGuid(),
                    "Maria", "maria@shopflow.test", "(11) 98888-7777",
                    2, 10m, 20m, "Camisa", "SKU-A",
                    null, null, null, null, null, null, null, null, null)
            ], 1));

        var payments = new Mock<IAdminOrderPixPaymentReader>();
        payments.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        var sut = new GetAdminProductDemandOrdersQueryHandler(readModel.Object, payments.Object);
        var result = await sut.Handle(new GetAdminProductDemandOrdersQuery(skuId), CancellationToken.None);

        var row = result.Items.Should().ContainSingle().Subject;
        row.IsGuest.Should().BeFalse();
        row.PhoneDigits.Should().Be("11988887777");
        row.Payment.Should().BeNull();
        row.Quantity.Should().Be(2);
        row.SkuId.Should().Be(skuId);
    }
}
