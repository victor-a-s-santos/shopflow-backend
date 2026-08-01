using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Commands;

public sealed record CreateDeliveryBatchCommand(
    IReadOnlyList<Guid> OrderIds,
    Guid? AdminId,
    string? DeliveryMethod = null,
    string? TrackingCode = null,
    string? InternalNote = null,
    bool ConfirmDifferentAddresses = false) : ICommand<DeliveryBatchDetailDto>;

public sealed record ShipDeliveryBatchCommand(
    Guid BatchId,
    Guid? AdminId,
    string? DeliveryMethod = null,
    string? TrackingCode = null,
    string? InternalNote = null) : ICommand<DeliveryBatchDetailDto>;

public sealed record DeliverDeliveryBatchCommand(
    Guid BatchId,
    Guid? AdminId,
    string? InternalNote = null) : ICommand<DeliveryBatchDetailDto>;

public sealed record UpdateDeliveryBatchInternalNoteCommand(
    Guid BatchId,
    string? InternalNote) : ICommand<DeliveryBatchDetailDto>;
