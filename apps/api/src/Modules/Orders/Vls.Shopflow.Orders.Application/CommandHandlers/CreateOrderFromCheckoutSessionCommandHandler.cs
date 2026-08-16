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
using Vls.Shopflow.Orders.Domain.Enums;
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
    IOrderEmailIntentRepository emailIntents,
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
            .Select(i => OrderItem.Create(
                i.SkuId,
                i.ProductName,
                i.SkuCode,
                i.Quantity,
                i.UnitPrice,
                ToOrderItemSalesSnapshot(i)))
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

        DeliveryMethod? preferredMethod = null;
        if (!string.IsNullOrWhiteSpace(session.PreferredDeliveryMethod)
            && Enum.TryParse<DeliveryMethod>(session.PreferredDeliveryMethod.Trim(), ignoreCase: true, out var parsed))
        {
            preferredMethod = parsed;
        }

        order.SetDeliveryPreference(
            preferredMethod,
            session.PreferredDeliveryDate,
            session.CustomerOrderNote);

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

        await emailIntents.EnsurePendingAsync(
            OrderEmailIntentFactory.PendingFromOrder(order, OrderEmailIntentType.OrderCreated, rawToken),
            cancellationToken);

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

    /// <summary>
    /// Copies snapshot from checkout. Pre-migration sessions without SalesMode fall back to Unit
    /// (do not re-read live catalog — avoids rewriting historical display).
    /// </summary>
    private static OrderItemSalesSnapshot ToOrderItemSalesSnapshot(CheckoutSessionItemSnapshot i)
        => new(
            SalesMode: string.IsNullOrWhiteSpace(i.SalesMode) ? "Unit" : i.SalesMode,
            PackageSize: i.PackageSize,
            PackageLabel: i.PackageLabel,
            PackageDescription: i.PackageDescription,
            QuantityUnitLabel: i.QuantityUnitLabel,
            ShowTotalPieces: i.ShowTotalPieces,
            TotalPieces: i.TotalPieces,
            EquivalentUnitPrice: i.EquivalentUnitPrice,
            SalesDisplaySummary: i.SalesDisplaySummary);
}
