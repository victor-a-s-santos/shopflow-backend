using MediatR;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Services;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.CommandHandlers;

public sealed class ShipOrderFulfillmentCommandHandler(
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IDeliveryBatchRepository batchRepository,
    IOrdersUnitOfWork unitOfWork,
    IOrderEmailIntentRepository emailIntents,
    IOptions<FulfillmentOptions> fulfillmentOptions)
    : IRequestHandler<ShipOrderFulfillmentCommand, AdminOrderDetailDto>
{
    public async Task<AdminOrderDetailDto> Handle(
        ShipOrderFulfillmentCommand command,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(command.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundException(command.OrderId);

        OrderStockConfirmationRules.EnsureConfirmedForShipment(
            order,
            fulfillmentOptions.Value.RequireStockConfirmation);

        DeliveryMethod? finalMethod = null;
        if (!string.IsNullOrWhiteSpace(command.FinalDeliveryMethod)
            && Enum.TryParse<DeliveryMethod>(command.FinalDeliveryMethod.Trim(), ignoreCase: true, out var parsed))
        {
            finalMethod = parsed;
        }

        order.MarkAsShipped(
            command.AdminId,
            finalMethod,
            command.TrackingCode,
            command.InternalNote);

        await emailIntents.EnsurePendingAsync(
            OrderEmailIntentFactory.PendingFromOrder(order, OrderEmailIntentType.OrderShipped),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payment = await pixPaymentReader.GetLatestByOrderIdAsync(order.Id, cancellationToken);
        var membership = await batchRepository.FindMembershipByOrderIdAsync(order.Id, cancellationToken);
        return AdminOrderMapper.ToDetailDto(
            order,
            payment,
            membership?.DeliveryBatchId,
            membership is null ? null : membership.BatchNumber.ToString(),
            fulfillmentOptions.Value.RequireStockConfirmation);
    }
}

public sealed class DeliverOrderFulfillmentCommandHandler(
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IDeliveryBatchRepository batchRepository,
    IOrdersUnitOfWork unitOfWork,
    IOrderEmailIntentRepository emailIntents,
    IOptions<FulfillmentOptions> fulfillmentOptions)
    : IRequestHandler<DeliverOrderFulfillmentCommand, AdminOrderDetailDto>
{
    public async Task<AdminOrderDetailDto> Handle(
        DeliverOrderFulfillmentCommand command,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(command.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundException(command.OrderId);

        order.MarkAsDelivered(command.AdminId, command.InternalNote);

        await emailIntents.EnsurePendingAsync(
            OrderEmailIntentFactory.PendingFromOrder(order, OrderEmailIntentType.OrderDelivered),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payment = await pixPaymentReader.GetLatestByOrderIdAsync(order.Id, cancellationToken);
        var membership = await batchRepository.FindMembershipByOrderIdAsync(order.Id, cancellationToken);
        return AdminOrderMapper.ToDetailDto(
            order,
            payment,
            membership?.DeliveryBatchId,
            membership is null ? null : membership.BatchNumber.ToString(),
            fulfillmentOptions.Value.RequireStockConfirmation);
    }
}

public sealed class ConfirmOrderStockCommandHandler(
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IDeliveryBatchRepository batchRepository,
    IOrdersUnitOfWork unitOfWork,
    IOrderEmailIntentRepository emailIntents,
    IOptions<FulfillmentOptions> fulfillmentOptions)
    : IRequestHandler<ConfirmOrderStockCommand, AdminOrderDetailDto>
{
    public async Task<AdminOrderDetailDto> Handle(
        ConfirmOrderStockCommand command,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(command.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundException(command.OrderId);

        var newlyConfirmed = order.ConfirmStock(command.AdminId, command.Note);

        if (newlyConfirmed)
        {
            await emailIntents.EnsurePendingAsync(
                OrderEmailIntentFactory.PendingFromOrder(order, OrderEmailIntentType.OrderStockConfirmed),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payment = await pixPaymentReader.GetLatestByOrderIdAsync(order.Id, cancellationToken);
        var membership = await batchRepository.FindMembershipByOrderIdAsync(order.Id, cancellationToken);
        return AdminOrderMapper.ToDetailDto(
            order,
            payment,
            membership?.DeliveryBatchId,
            membership is null ? null : membership.BatchNumber.ToString(),
            fulfillmentOptions.Value.RequireStockConfirmation);
    }
}

public sealed class UpdateOrderInternalNoteCommandHandler(
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IDeliveryBatchRepository batchRepository,
    IOrdersUnitOfWork unitOfWork,
    IOptions<FulfillmentOptions> fulfillmentOptions)
    : IRequestHandler<UpdateOrderInternalNoteCommand, AdminOrderDetailDto>
{
    public async Task<AdminOrderDetailDto> Handle(
        UpdateOrderInternalNoteCommand command,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(command.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundException(command.OrderId);

        order.SetInternalOrderNote(command.InternalNote);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payment = await pixPaymentReader.GetLatestByOrderIdAsync(order.Id, cancellationToken);
        var membership = await batchRepository.FindMembershipByOrderIdAsync(order.Id, cancellationToken);
        return AdminOrderMapper.ToDetailDto(
            order,
            payment,
            membership?.DeliveryBatchId,
            membership is null ? null : membership.BatchNumber.ToString(),
            fulfillmentOptions.Value.RequireStockConfirmation);
    }
}
