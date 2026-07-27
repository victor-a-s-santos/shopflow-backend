using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Application.Commands;

public sealed record CreateVariantProductCommand(
    string Name,
    string? Slug,
    Guid? CategoryId,
    bool IsFeatured = false,
    int? DisplayOrder = null,
    string? Description = null,
    /// <summary>When null, defaults to true. Explicit false persists inactive.</summary>
    bool? IsActive = null) : ICommand<Guid>;
