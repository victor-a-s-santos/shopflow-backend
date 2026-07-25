using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.QueryHandlers;

public sealed class GetAdminProductsQueryHandler(IAdminProductReadModel readModel)
    : IQueryHandler<GetAdminProductsQuery, PagedAdminProductsDto>
{
    public Task<PagedAdminProductsDto> Handle(GetAdminProductsQuery query, CancellationToken cancellationToken)
        => readModel.GetPagedAsync(
            query.Page,
            query.PageSize,
            query.Sort,
            query.Q,
            query.CategorySlug,
            query.CategoryId,
            query.Status,
            query.Featured,
            cancellationToken);
}
