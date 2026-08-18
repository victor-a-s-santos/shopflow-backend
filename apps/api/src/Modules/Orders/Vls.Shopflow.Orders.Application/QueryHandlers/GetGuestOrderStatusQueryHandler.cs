using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Constants;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.QueryHandlers;

internal static class GuestOrderStatusResponseBuilder
{
    public static async Task<GuestOrderStatusDto> BuildAsync(
        GuestOrderAccessToken accessToken,
        Order order,
        IOrderPixPaymentStatusReader paymentStatusReader,
        ICustomerAccountPort customerAccountPort,
        IOrdersUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var payment = await paymentStatusReader.GetLatestByOrderIdAsync(order.Id, cancellationToken);

        if (order.Status == OrderStatus.Paid
            && payment is not null
            && !string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Guest status inconsistency for order {OrderId}: Order is Paid but Pix status is {PixStatus}.",
                order.Id,
                payment.Status);
        }

        if (order.Status == OrderStatus.PendingPayment
            && payment is not null
            && string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Guest status inconsistency for order {OrderId}: Pix is Paid but Order is still PendingPayment.",
                order.Id);
        }

        accessToken.MarkUsed(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var canCreateAccount = order.CustomerUserId is null;
        var accountExistsForEmail = canCreateAccount
            && await customerAccountPort.EmailExistsAsync(order.CustomerEmail, cancellationToken);

        return OrderMapper.ToGuestStatusDto(
            order,
            payment,
            accessToken,
            canCreateAccount,
            accountExistsForEmail);
    }
}

public sealed class GetGuestOrderStatusQueryHandler(
    IGuestOrderAccessGate guestOrderAccessGate,
    IOrderPixPaymentStatusReader paymentStatusReader,
    ICustomerAccountPort customerAccountPort,
    IOrdersUnitOfWork unitOfWork,
    ILogger<GetGuestOrderStatusQueryHandler> logger)
    : IRequestHandler<GetGuestOrderStatusQuery, GuestOrderStatusDto>
{
    public async Task<GuestOrderStatusDto> Handle(
        GetGuestOrderStatusQuery query,
        CancellationToken cancellationToken)
    {
        var (accessToken, order) = await guestOrderAccessGate.ValidateAsync(
            query.OrderId,
            query.AccessToken,
            cancellationToken);

        return await GuestOrderStatusResponseBuilder.BuildAsync(
            accessToken,
            order,
            paymentStatusReader,
            customerAccountPort,
            unitOfWork,
            logger,
            cancellationToken);
    }
}

public sealed class GetPublicOrderStatusQueryHandler(
    IGuestOrderAccessGate guestOrderAccessGate,
    IOrderPixPaymentStatusReader paymentStatusReader,
    ICustomerAccountPort customerAccountPort,
    IOrdersUnitOfWork unitOfWork,
    ILogger<GetPublicOrderStatusQueryHandler> logger)
    : IRequestHandler<GetPublicOrderStatusQuery, GuestOrderStatusDto>
{
    public async Task<GuestOrderStatusDto> Handle(
        GetPublicOrderStatusQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryParseOrderNumber(query.OrderNumber, out var orderNumber))
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.OrderNotFoundOrAccessDenied);

        var (accessToken, order) = await guestOrderAccessGate.ValidateByOrderNumberAsync(
            orderNumber,
            query.AccessToken,
            cancellationToken);

        return await GuestOrderStatusResponseBuilder.BuildAsync(
            accessToken,
            order,
            paymentStatusReader,
            customerAccountPort,
            unitOfWork,
            logger,
            cancellationToken);
    }

    private static bool TryParseOrderNumber(string? raw, out long orderNumber)
    {
        orderNumber = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim().TrimStart('#');
        return long.TryParse(value, System.Globalization.NumberStyles.None, null, out orderNumber)
               && orderNumber > 0;
    }
}
