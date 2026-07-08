using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Application.Interfaces;

public interface ISlugService
{
    Task<Slug> EnsureUniqueAsync(Slug candidate, CancellationToken cancellationToken);
    Task<Slug> EnsureUniqueAsync(Slug candidate, Guid? excludeProductId, CancellationToken cancellationToken);
}