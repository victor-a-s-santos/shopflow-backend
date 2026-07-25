using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Application.Commands;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Slug,
    Guid? CategoryId,
    bool IsActive,
    /// <summary>When null, preserve current storefront display settings.</summary>
    bool? IsFeatured = null,
    /// <summary>When null together with <see cref="IsFeatured"/>, preserve. When IsFeatured is set, applies (null = clear order).</summary>
    int? DisplayOrder = null,
    bool UpdateDisplaySettings = false) : ICommand;
