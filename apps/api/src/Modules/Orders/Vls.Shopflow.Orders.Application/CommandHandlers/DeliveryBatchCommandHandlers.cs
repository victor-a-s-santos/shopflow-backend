using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Services;
using Vls.Shopflow.Orders.Domain.Constants;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.CommandHandlers;

public sealed class CreateDeliveryBatchCommandHandler(
    IOrderRepository orderRepository,
    IDeliveryBatchRepository batchRepository,
    IDeliveryBatchNumberGenerator batchNumberGenerator,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IOrdersUnitOfWork unitOfWork,
    ILogger<CreateDeliveryBatchCommandHandler> logger)
    : IRequestHandler<CreateDeliveryBatchCommand, DeliveryBatchDetailDto>
{
    public async Task<DeliveryBatchDetailDto> Handle(
        CreateDeliveryBatchCommand command,
        CancellationToken cancellationToken)
    {
        var orderIds = command.OrderIds.Distinct().ToList();
        var orders = await orderRepository.GetByIdsWithItemsAsync(orderIds, cancellationToken);
        if (orders.Count != orderIds.Count)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderNotFound,
                "Um ou mais pedidos não foram encontrados.");
        }

        var alreadyInBatch = await batchRepository.GetOrderIdsInAnyBatchAsync(orderIds, cancellationToken);
        foreach (var order in orders)
            DeliveryBatchGroupingRules.EnsureEligibleForBatch(order, alreadyInBatch.Contains(order.Id));

        var identity = DeliveryBatchGroupingRules.ResolveIdentity(orders);
        var addresses = DeliveryBatchGroupingRules.BuildAddressInfos(orders);
        var hasDifferentAddresses = DeliveryBatchGroupingRules.HasDifferentAddresses(addresses);

        if (hasDifferentAddresses && !command.ConfirmDifferentAddresses)
        {
            throw new DeliveryBatchAddressMismatchException(
                addresses.Select(a => new
                {
                    orderId = a.OrderId,
                    orderNumber = a.OrderNumber,
                    addressSummary = a.AddressSummary
                }).Cast<object>().ToList());
        }

        DeliveryMethod? method = null;
        if (!string.IsNullOrWhiteSpace(command.DeliveryMethod)
            && Enum.TryParse<DeliveryMethod>(command.DeliveryMethod.Trim(), ignoreCase: true, out var parsed))
        {
            method = parsed;
        }

        var batch = DeliveryBatch.CreateAwaitingShipment(
            orderIds,
            identity.CustomerUserId,
            identity.Name,
            identity.Email,
            identity.Phone,
            hasDifferentAddresses,
            command.AdminId,
            method,
            command.TrackingCode,
            command.InternalNote);

        var batchNumber = await batchNumberGenerator.NextAsync(cancellationToken);
        batch.AssignBatchNumber(batchNumber);

        await batchRepository.AddAsync(batch, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created delivery batch {BatchId} (#{BatchNumber}) with {OrderCount} orders",
            batch.Id,
            batch.BatchNumber,
            orderIds.Count);

        return await BuildDetailAsync(batch, orders, cancellationToken);
    }

    private async Task<DeliveryBatchDetailDto> BuildDetailAsync(
        DeliveryBatch batch,
        IReadOnlyList<Order> orders,
        CancellationToken cancellationToken)
    {
        var payments = await pixPaymentReader.GetLatestByOrderIdsAsync(
            orders.Select(o => o.Id).ToList(),
            cancellationToken);
        var paymentStatuses = payments.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.Status);
        return DeliveryBatchMapper.ToDetailDto(batch, orders, paymentStatuses);
    }
}

public sealed class ShipDeliveryBatchCommandHandler(
    IDeliveryBatchRepository batchRepository,
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IOrdersUnitOfWork unitOfWork)
    : IRequestHandler<ShipDeliveryBatchCommand, DeliveryBatchDetailDto>
{
    public async Task<DeliveryBatchDetailDto> Handle(
        ShipDeliveryBatchCommand command,
        CancellationToken cancellationToken)
    {
        var batch = await batchRepository.GetByIdWithOrdersAsync(command.BatchId, cancellationToken)
                    ?? throw new DeliveryBatchNotFoundException(command.BatchId);

        var orderIds = batch.Orders.Select(o => o.OrderId).ToList();
        var orders = await orderRepository.GetByIdsWithItemsAsync(orderIds, cancellationToken);
        if (orders.Count != orderIds.Count)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderNotFound,
                "Um ou mais pedidos da remessa não foram encontrados.");
        }

        foreach (var order in orders)
        {
            if (order.Status != OrderStatus.Paid)
            {
                throw new DeliveryBatchException(
                    DeliveryBatchErrorCodes.CannotBeShipped,
                    "Esta entrega agrupada não pode ser marcada como enviada.");
            }

            if (order.FulfillmentStatus == FulfillmentStatus.Delivered)
            {
                throw new DeliveryBatchException(
                    DeliveryBatchErrorCodes.CannotBeShipped,
                    "Esta entrega agrupada não pode ser marcada como enviada.");
            }
        }

        DeliveryMethod? method = null;
        if (!string.IsNullOrWhiteSpace(command.DeliveryMethod)
            && Enum.TryParse<DeliveryMethod>(command.DeliveryMethod.Trim(), ignoreCase: true, out var parsed))
        {
            method = parsed;
        }

        // Batch note stays on the batch — do not overwrite per-order InternalOrderNote.
        batch.MarkAsShipped(command.AdminId, method, command.TrackingCode, command.InternalNote);

        var shipMethod = batch.DeliveryMethod;
        var tracking = batch.TrackingCode;
        foreach (var order in orders)
            order.MarkAsShipped(command.AdminId, shipMethod, tracking, internalNote: null);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payments = await pixPaymentReader.GetLatestByOrderIdsAsync(orderIds, cancellationToken);
        var paymentStatuses = payments.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.Status);
        return DeliveryBatchMapper.ToDetailDto(batch, orders, paymentStatuses);
    }
}

public sealed class DeliverDeliveryBatchCommandHandler(
    IDeliveryBatchRepository batchRepository,
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IOrdersUnitOfWork unitOfWork)
    : IRequestHandler<DeliverDeliveryBatchCommand, DeliveryBatchDetailDto>
{
    public async Task<DeliveryBatchDetailDto> Handle(
        DeliverDeliveryBatchCommand command,
        CancellationToken cancellationToken)
    {
        var batch = await batchRepository.GetByIdWithOrdersAsync(command.BatchId, cancellationToken)
                    ?? throw new DeliveryBatchNotFoundException(command.BatchId);

        var orderIds = batch.Orders.Select(o => o.OrderId).ToList();
        var orders = await orderRepository.GetByIdsWithItemsAsync(orderIds, cancellationToken);
        if (orders.Count != orderIds.Count)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderNotFound,
                "Um ou mais pedidos da remessa não foram encontrados.");
        }

        foreach (var order in orders)
        {
            if (order.Status != OrderStatus.Paid
                || order.FulfillmentStatus is not (FulfillmentStatus.Shipped or FulfillmentStatus.Delivered))
            {
                throw new DeliveryBatchException(
                    DeliveryBatchErrorCodes.CannotBeDelivered,
                    "A entrega agrupada precisa estar marcada como enviada antes de ser entregue.");
            }
        }

        batch.MarkAsDelivered(command.AdminId, command.InternalNote);

        foreach (var order in orders)
            order.MarkAsDelivered(command.AdminId, internalNote: null);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payments = await pixPaymentReader.GetLatestByOrderIdsAsync(orderIds, cancellationToken);
        var paymentStatuses = payments.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.Status);
        return DeliveryBatchMapper.ToDetailDto(batch, orders, paymentStatuses);
    }
}

public sealed class UpdateDeliveryBatchInternalNoteCommandHandler(
    IDeliveryBatchRepository batchRepository,
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader,
    IOrdersUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDeliveryBatchInternalNoteCommand, DeliveryBatchDetailDto>
{
    public async Task<DeliveryBatchDetailDto> Handle(
        UpdateDeliveryBatchInternalNoteCommand command,
        CancellationToken cancellationToken)
    {
        var batch = await batchRepository.GetByIdWithOrdersAsync(command.BatchId, cancellationToken)
                    ?? throw new DeliveryBatchNotFoundException(command.BatchId);

        batch.SetInternalNote(command.InternalNote);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var orderIds = batch.Orders.Select(o => o.OrderId).ToList();
        var orders = await orderRepository.GetByIdsWithItemsAsync(orderIds, cancellationToken);
        var payments = await pixPaymentReader.GetLatestByOrderIdsAsync(orderIds, cancellationToken);
        var paymentStatuses = payments.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.Status);
        return DeliveryBatchMapper.ToDetailDto(batch, orders, paymentStatuses);
    }
}
