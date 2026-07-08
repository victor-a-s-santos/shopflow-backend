using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Application.QueryHandlers;

public sealed class GetAllAttributeDefinitionsQueryHandler(IAttributeDefinitionReadModel readModel)
    : IQueryHandler<GetAllAttributeDefinitionsQuery, IReadOnlyList<AttributeDefinitionDto>>
{
    public Task<IReadOnlyList<AttributeDefinitionDto>> Handle(
        GetAllAttributeDefinitionsQuery query,
        CancellationToken ct)
        => readModel.GetAllAsync(ct);
}