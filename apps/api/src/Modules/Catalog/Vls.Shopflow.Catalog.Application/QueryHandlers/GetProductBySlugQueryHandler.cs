using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.QueryHandlers;

public sealed class GetProductBySlugQueryHandler(
    IProductReadModel readModel
) : IQueryHandler<GetProductBySlugQuery, ProductDetailedDto?>
{
    public Task<ProductDetailedDto?> Handle(GetProductBySlugQuery query, CancellationToken cancellationToken)
        => readModel.GetBySlugAsync(query.Slug, cancellationToken);
}
