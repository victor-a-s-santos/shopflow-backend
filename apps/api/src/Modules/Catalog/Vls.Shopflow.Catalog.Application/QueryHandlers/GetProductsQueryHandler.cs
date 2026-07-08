using MediatR;
using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.QueryHandlers;

public sealed class GetProductsQueryHandler(
    IProductReadModel readModel
) : IQueryHandler<GetProductsQuery, PagedProductsDto>
{
    public async Task<PagedProductsDto> Handle(GetProductsQuery query, CancellationToken cancellationToken)
    {
        return await readModel.GetPagedAsync(query.Page, query.PageSize, cancellationToken);
    }
}