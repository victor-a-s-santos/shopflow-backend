using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Application.Commands;

public sealed record DeleteProductCommand(Guid ProductId) : ICommand;
