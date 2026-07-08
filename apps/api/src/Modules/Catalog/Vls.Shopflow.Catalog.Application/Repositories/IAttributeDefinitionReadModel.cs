using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Repositories;

public interface IAttributeDefinitionReadModel
{
    Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync(CancellationToken ct = default);
}