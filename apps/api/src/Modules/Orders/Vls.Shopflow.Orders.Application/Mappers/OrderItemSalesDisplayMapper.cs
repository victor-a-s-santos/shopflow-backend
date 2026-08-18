using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Mappers;

public static class OrderItemSalesDisplayMapper
{
    /// <summary>
    /// Package modes get a filled DTO; Unit/Min/Multiple and legacy null snapshots return null.
    /// </summary>
    public static OrderItemSalesDisplayDto? ToDto(OrderItem item)
    {
        if (item.PackageSize is not { } size || size <= 1)
            return null;

        var mode = item.SalesMode;
        if (string.IsNullOrWhiteSpace(mode)
            || (!string.Equals(mode, "FixedPackage", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mode, "AssortedPackage", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return new OrderItemSalesDisplayDto(
            SalesMode: mode,
            PackageSize: size,
            PackageLabel: item.PackageLabel,
            PackageDescription: item.PackageDescription,
            QuantityUnitLabel: item.QuantityUnitLabel,
            ShowTotalPieces: item.ShowTotalPieces ?? true,
            TotalPieces: item.TotalPieces,
            EquivalentUnitPrice: item.EquivalentUnitPrice,
            Summary: item.SalesDisplaySummary);
    }
}
