using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Enums;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Domain.Entities;

public sealed class StockMovement : Entity<Guid>
{
    public Guid InventoryItemId { get; private set; }
    public InventoryItem InventoryItem { get; private set; } = default!;

    public Guid SkuId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private StockMovement() { }

    public static StockMovement Create(
        Guid inventoryItemId,
        Guid skuId,
        StockMovementType type,
        int quantity,
        string? reason)
    {
        if (quantity <= 0)
            throw new InvalidStockQuantityException("Movement quantity must be greater than zero.");

        return new StockMovement
        {
            Id = Guid.NewGuid(),
            InventoryItemId = inventoryItemId,
            SkuId = skuId,
            Type = type,
            Quantity = quantity,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
