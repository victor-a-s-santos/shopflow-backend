using Vls.Shopflow.Inventory.Application.Queries;

namespace Vls.Shopflow.Inventory.Application.Services;

/// <summary>
/// Computes display stock status for Admin Inventory SKU rows.
/// Threshold default: <see cref="AdminInventorySkuListFilters.LowStockThreshold"/>.
/// </summary>
public static class AdminInventoryStockStatus
{
    public static string Compute(
        int availableQuantity,
        int reservedQuantity,
        int lowStockThreshold = AdminInventorySkuListFilters.LowStockThreshold)
    {
        if (availableQuantity <= 0 && reservedQuantity > 0)
            return AdminInventorySkuListFilters.StockReserved;

        if (availableQuantity <= 0)
            return AdminInventorySkuListFilters.StockOutOfStock;

        if (availableQuantity <= lowStockThreshold)
            return AdminInventorySkuListFilters.StockLowStock;

        return AdminInventorySkuListFilters.StockInStock;
    }
}
