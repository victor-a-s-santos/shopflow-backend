using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.CommandHandlers;

public sealed class CreateOrderFromCheckoutSessionCommandHandler(
    ICheckoutSessionReader checkoutSessionReader,
    IOrderRepository orderRepository,
    IOrderNumberGenerator orderNumberGenerator,
    IGuestOrderAccessTokenRepository guestTokenRepository,
    IGuestOrderAccessTokenHasher tokenHasher,
    IOrdersUnitOfWork unitOfWork,
    IOptions<GuestOrderAccessOptions> guestOrderAccessOptions,
    ILogger<CreateOrderFromCheckoutSessionCommandHandler> logger)
    : IRequestHandler<CreateOrderFromCheckoutSessionCommand, OrderDto>
{
    public async Task<OrderDto> Handle(
        CreateOrderFromCheckoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await orderRepository.GetByCheckoutSessionIdWithItemsAsync(
            command.CheckoutSessionId,
            cancellationToken);

        if (existing is not null)
            throw new OrderAlreadyExistsForCheckoutSessionException(command.CheckoutSessionId, existing.Id);

        var session = await checkoutSessionReader.GetByIdAsync(command.CheckoutSessionId, cancellationToken)
                      ?? throw new CheckoutSessionNotFoundForOrderException(command.CheckoutSessionId);

        OrderMapper.EnsureCheckoutSessionCanCreateOrder(session.Status, session.Id);

        var items = session.Items
            .Select(i => OrderItem.Create(i.SkuId, i.ProductName, i.SkuCode, i.Quantity, i.UnitPrice))
            .ToList();

        var order = Order.CreatePendingPayment(
            session.Id,
            session.CustomerFullName,
            session.CustomerEmail,
            session.CustomerPhone,
            session.ShippingZipCode,
            session.ShippingStreet,
            session.ShippingNumber,
            session.ShippingComplement,
            session.ShippingNeighborhood,
            session.ShippingCity,
            session.ShippingState,
            session.Subtotal,
            session.ShippingAmount,
            session.Total,
            items,
            command.CustomerUserId);

        var orderNumber = await orderNumberGenerator.NextAsync(cancellationToken);
        order.AssignOrderNumber(orderNumber);

        await orderRepository.AddAsync(order, cancellationToken);

        string? rawToken = null;
        DateTimeOffset? tokenExpiresAt = null;
        var options = guestOrderAccessOptions.Value;

        if (options.Enabled)
        {
            var ttlDays = Math.Max(1, options.TokenTtlDays);
            tokenExpiresAt = DateTimeOffset.UtcNow.AddDays(ttlDays);
            rawToken = tokenHasher.GenerateRawToken();
            var hash = tokenHasher.Hash(rawToken);
            var accessToken = GuestOrderAccessToken.Create(order.Id, hash, tokenExpiresAt.Value);
            await guestTokenRepository.AddAsync(accessToken, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created order {OrderId} (#{OrderNumber}) from checkout session {CheckoutSessionId} with status PendingPayment. " +
            "GuestAccessTokenIssued={TokenIssued} CustomerUserIdBound={CustomerBound}",
            order.Id,
            order.OrderNumber,
            session.Id,
            rawToken is not null,
            order.CustomerUserId is not null);

        return OrderMapper.ToDto(order, rawToken, tokenExpiresAt);
    }
}
