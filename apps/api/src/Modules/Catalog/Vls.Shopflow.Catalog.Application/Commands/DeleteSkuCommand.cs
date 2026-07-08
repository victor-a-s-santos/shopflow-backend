using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Application.Commands;

public sealed record DeleteSkuCommand(Guid ProductId, Guid SkuId) : ICommand;
