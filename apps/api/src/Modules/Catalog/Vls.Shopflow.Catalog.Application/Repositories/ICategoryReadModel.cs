using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Repositories;

public interface ICategoryReadModel
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
}