using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Queries;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Application.QueryHandlers;

public sealed class GetAdminInventorySkusQueryHandler(IAdminInventorySkuReadModel readModel)
    : IQueryHandler<GetAdminInventorySkusQuery, PagedAdminInventorySkusDto>
{
    public Task<PagedAdminInventorySkusDto> Handle(
        GetAdminInventorySkusQuery query,
        CancellationToken cancellationToken)
        => readModel.GetPagedAsync(
            query.Page,
            query.PageSize,
            query.Sort,
            query.Q,
            query.ProductId,
            query.CategorySlug,
            query.CategoryId,
            query.Status,
            query.StockStatus,
            cancellationToken);
}
