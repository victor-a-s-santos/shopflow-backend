using MediatR;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.CommandHandlers;

public sealed class ShipOrderFulfillmentCommandHandler(
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IDeliveryBatchRepository batchRepository,
    IOrdersUnitOfWork unitOfWork,
    IOrderEmailIntentRepository emailIntents)
    : IRequestHandler<ShipOrderFulfillmentCommand, AdminOrderDetailDto>
{
    public async Task<AdminOrderDetailDto> Handle(
        ShipOrderFulfillmentCommand command,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(command.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundException(command.OrderId);

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
            membership is null ? null : membership.BatchNumber.ToString());
    }
}

public sealed class DeliverOrderFulfillmentCommandHandler(
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IDeliveryBatchRepository batchRepository,
    IOrdersUnitOfWork unitOfWork,
    IOrderEmailIntentRepository emailIntents)
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
            membership is null ? null : membership.BatchNumber.ToString());
    }
}

public sealed class UpdateOrderInternalNoteCommandHandler(
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IDeliveryBatchRepository batchRepository,
    IOrdersUnitOfWork unitOfWork)
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
            membership is null ? null : membership.BatchNumber.ToString());
    }
}
