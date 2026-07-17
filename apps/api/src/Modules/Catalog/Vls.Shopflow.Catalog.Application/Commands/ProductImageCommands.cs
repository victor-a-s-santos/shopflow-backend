using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Application.Commands;

public sealed record DeleteProductImageCommand(Guid ProductId, Guid ImageId) : ICommand;

public sealed record SetPrimaryProductImageCommand(Guid ProductId, Guid ImageId) : ICommand;
