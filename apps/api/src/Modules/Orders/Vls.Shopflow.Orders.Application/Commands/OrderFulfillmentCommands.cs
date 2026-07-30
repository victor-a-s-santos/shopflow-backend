using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Commands;

public sealed record ShipOrderFulfillmentCommand(
    Guid OrderId,
    Guid? AdminId,
    string? FinalDeliveryMethod = null,
    string? TrackingCode = null,
    string? InternalNote = null) : ICommand<AdminOrderDetailDto>;

public sealed record DeliverOrderFulfillmentCommand(
    Guid OrderId,
    Guid? AdminId,
    string? InternalNote = null) : ICommand<AdminOrderDetailDto>;

public sealed record UpdateOrderInternalNoteCommand(
    Guid OrderId,
    string? InternalNote) : ICommand<AdminOrderDetailDto>;
