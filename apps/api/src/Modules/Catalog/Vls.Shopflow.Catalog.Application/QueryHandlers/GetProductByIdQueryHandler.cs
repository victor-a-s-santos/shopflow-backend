using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.QueryHandlers;

public sealed class GetProductByIdQueryHandler(
    IProductReadModel readModel
) : IQueryHandler<GetProductByIdQuery, ProductDetailedDto?>
{
    public async Task<ProductDetailedDto?> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        return await readModel.GetByIdAsync(query.Id, cancellationToken);
    }
}