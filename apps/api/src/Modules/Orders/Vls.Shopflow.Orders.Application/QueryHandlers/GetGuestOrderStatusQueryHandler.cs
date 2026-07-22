using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.QueryHandlers;

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
