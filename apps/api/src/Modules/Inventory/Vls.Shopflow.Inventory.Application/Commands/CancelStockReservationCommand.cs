using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Inventory.Application.Commands;

public sealed record CancelStockReservationCommand(Guid ReservationId) : ICommand;
