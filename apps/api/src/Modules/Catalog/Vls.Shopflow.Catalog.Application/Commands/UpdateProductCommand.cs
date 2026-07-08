using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Application.Commands;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Slug,
    Guid? CategoryId,
    bool IsActive) : ICommand;
