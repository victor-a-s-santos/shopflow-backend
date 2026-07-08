using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Queries;

public sealed record GetProductsQuery(int Page, int PageSize) : IQuery<PagedProductsDto>;
