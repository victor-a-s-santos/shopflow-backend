using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Mappers;

public static class OrderItemSalesDisplayMapper
{
    /// <summary>
    /// Package modes get a filled DTO; Unit/Min/Multiple and legacy null snapshots return null.
    /// </summary>
    public static OrderItemSalesDisplayDto? ToDto(OrderItem item)
        => ToDto(
            item.SalesMode,
            item.PackageSize,
            item.PackageLabel,
            item.PackageDescription,
            item.QuantityUnitLabel,
            item.ShowTotalPieces,
            item.TotalPieces,
            item.EquivalentUnitPrice,
            item.SalesDisplaySummary);

    public static OrderItemSalesDisplayDto? ToDto(
        string? salesMode,
        int? packageSize,
        string? packageLabel,
        string? packageDescription,
        string? quantityUnitLabel,
        bool? showTotalPieces,
        int? totalPieces,
        decimal? equivalentUnitPrice,
        string? summary)
    {
        if (packageSize is not { } size || size <= 1)
            return null;

        if (string.IsNullOrWhiteSpace(salesMode)
            || (!string.Equals(salesMode, "FixedPackage", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(salesMode, "AssortedPackage", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return new OrderItemSalesDisplayDto(
            SalesMode: salesMode,
            PackageSize: size,
            PackageLabel: packageLabel,
            PackageDescription: packageDescription,
            QuantityUnitLabel: quantityUnitLabel,
            ShowTotalPieces: showTotalPieces ?? true,
            TotalPieces: totalPieces,
            EquivalentUnitPrice: equivalentUnitPrice,
            Summary: summary);
    }
}
