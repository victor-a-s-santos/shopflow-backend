using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.QueryHandlers;

public sealed class GetGuestOrderStatusQueryHandler(
    IOrderRepository orderRepository,
    IGuestOrderAccessTokenRepository guestTokenRepository,
    IGuestOrderAccessTokenHasher tokenHasher,
    IOrderPixPaymentStatusReader paymentStatusReader,
    IOrdersUnitOfWork unitOfWork,
    IOptions<GuestOrderAccessOptions> guestOrderAccessOptions,
    ILogger<GetGuestOrderStatusQueryHandler> logger)
    : IRequestHandler<GetGuestOrderStatusQuery, GuestOrderStatusDto>
{
    public async Task<GuestOrderStatusDto> Handle(
        GetGuestOrderStatusQuery query,
        CancellationToken cancellationToken)
    {
        if (!guestOrderAccessOptions.Value.Enabled)
            throw new GuestOrderAccessDeniedException();

        if (string.IsNullOrWhiteSpace(query.AccessToken))
            throw new GuestOrderAccessDeniedException();

        string tokenHash;
        try
        {
            tokenHash = tokenHasher.Hash(query.AccessToken);
        }
        catch (GuestOrderAccessMisconfiguredException)
        {
            throw;
        }
        catch
        {
            throw new GuestOrderAccessDeniedException();
        }

        var now = DateTimeOffset.UtcNow;
        var accessToken = await guestTokenRepository.FindActiveByOrderIdAndHashAsync(
            query.OrderId,
            tokenHash,
            now,
            cancellationToken);

        if (accessToken is null)
            throw new GuestOrderAccessDeniedException();

        var order = await orderRepository.GetByIdWithItemsAsync(query.OrderId, cancellationToken);
        if (order is null)
            throw new GuestOrderAccessDeniedException();

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

        accessToken.MarkUsed(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return OrderMapper.ToGuestStatusDto(order, payment, accessToken);
    }
}
