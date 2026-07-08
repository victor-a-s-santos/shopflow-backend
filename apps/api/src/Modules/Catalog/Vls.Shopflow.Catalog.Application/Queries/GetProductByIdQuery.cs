using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Queries;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<ProductDetailedDto?>;