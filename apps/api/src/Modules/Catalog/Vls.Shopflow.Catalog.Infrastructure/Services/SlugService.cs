using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Infrastructure.Services;

public sealed class SlugService(CatalogDbContext db) : ISlugService
{
    public async Task<Slug> EnsureUniqueAsync(Slug candidate, CancellationToken ct)
        => await EnsureUniqueAsync(candidate, null, ct);

    public async Task<Slug> EnsureUniqueAsync(Slug candidate, Guid? excludeProductId, CancellationToken ct)
    {
        var baseVal = candidate.Value;
        var slug = baseVal;
        var i = 2;

        bool exists = excludeProductId.HasValue
            ? await db.Products.AnyAsync(p => p.Slug.Value == slug && p.Id != excludeProductId, ct)
            : await db.Products.AnyAsync(p => p.Slug.Value == slug, ct);
        while (exists)
        {
            slug = $"{baseVal}-{i++}";
            exists = excludeProductId.HasValue
                ? await db.Products.AnyAsync(p => p.Slug.Value == slug && p.Id != excludeProductId, ct)
                : await db.Products.AnyAsync(p => p.Slug.Value == slug, ct);
        }

        return Slug.From(slug);
    }
}