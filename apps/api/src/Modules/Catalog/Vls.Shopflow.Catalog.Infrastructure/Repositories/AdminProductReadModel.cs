using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Infrastructure.Repositories;

/// <summary>
/// Backoffice product list — includes inactive / incomplete products.
/// Separate from the public storefront <see cref="ProductReadModel"/>.
/// </summary>
public sealed class AdminProductReadModel(CatalogDbContext db) : IAdminProductReadModel
{
    public async Task<PagedAdminProductsDto> GetPagedAsync(
        int page,
        int pageSize,
        string sort,
        string? q,
        string? categorySlug,
        Guid? categoryId,
        string status,
        string featured,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        sort = AdminProductListSort.Normalize(sort);
        status = AdminProductListFilters.NormalizeStatus(status);
        featured = AdminProductListFilters.NormalizeFeatured(featured);

        var query = db.Products.AsNoTracking().AsQueryable();

        if (status == AdminProductListFilters.StatusActive)
            query = query.Where(p => p.IsActive);
        else if (status == AdminProductListFilters.StatusInactive)
            query = query.Where(p => !p.IsActive);

        if (featured == AdminProductListFilters.FeaturedOnly)
            query = query.Where(p => p.IsFeatured);
        else if (featured == AdminProductListFilters.FeaturedNot)
            query = query.Where(p => !p.IsFeatured);

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var slug = categorySlug.Trim().ToLowerInvariant();
            query = query.Where(p => p.Category != null && p.Category.Slug.Value == slug);
        }

        if (categoryId is { } cid && cid != Guid.Empty)
            query = query.Where(p => p.CategoryId == cid);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term)
                || p.Slug.Value.ToLower().Contains(term)
                || p.Skus.Any(s => s.Code.ToLower().Contains(term)));
        }

        var totalItems = await query.CountAsync(ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var ordered = ApplySort(query, sort);

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                Slug = p.Slug.Value,
                p.IsActive,
                p.IsFeatured,
                p.DisplayOrder,
                p.CreatedAt,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                CategorySlug = p.Category != null ? p.Category.Slug.Value : null,
                PrimaryImageUrl = p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault()
                    ?? p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                SkuCount = p.Skus.Count(),
                ActiveSkuCount = p.Skus.Count(s => s.IsActive),
                MinPrice = p.Skus.Any(s => s.IsActive)
                    ? p.Skus.Where(s => s.IsActive)
                        .Min(s => s.Price.Promotional != null
                            ? (s.Price.Promotional.Amount < s.Price.Regular.Amount
                                ? s.Price.Promotional.Amount
                                : s.Price.Regular.Amount)
                            : s.Price.Regular.Amount)
                    : p.Skus.Any()
                        ? p.Skus.Min(s => s.Price.Promotional != null
                            ? (s.Price.Promotional.Amount < s.Price.Regular.Amount
                                ? s.Price.Promotional.Amount
                                : s.Price.Regular.Amount)
                            : s.Price.Regular.Amount)
                        : (decimal?)null,
                HasPromotionalPrice = p.Skus.Any(s => s.IsActive)
                    ? p.Skus.Any(s => s.IsActive && s.Price.Promotional != null)
                    : p.Skus.Any(s => s.Price.Promotional != null)
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminProductListItemDto(
            r.Id,
            r.Name,
            r.Slug,
            r.IsActive,
            r.IsFeatured,
            r.DisplayOrder,
            r.CreatedAt,
            r.CategoryId is { } id && r.CategoryName is not null && r.CategorySlug is not null
                ? new AdminProductCategoryDto(id, r.CategoryName, r.CategorySlug)
                : null,
            r.PrimaryImageUrl,
            r.SkuCount,
            r.ActiveSkuCount,
            r.MinPrice,
            r.HasPromotionalPrice)).ToList();

        return new PagedAdminProductsDto(
            Items: items,
            Page: page,
            PageSize: pageSize,
            TotalItems: totalItems,
            TotalPages: totalPages,
            HasNextPage: page < totalPages,
            HasPreviousPage: page > 1 && totalItems > 0);
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> query, string sort)
        => sort switch
        {
            AdminProductListSort.Oldest => query
                .OrderBy(p => p.CreatedAt)
                .ThenBy(p => p.Id),

            AdminProductListSort.NameAsc => query
                .OrderBy(p => p.Name)
                .ThenBy(p => p.Id),

            AdminProductListSort.NameDesc => query
                .OrderByDescending(p => p.Name)
                .ThenBy(p => p.Id),

            AdminProductListSort.DisplayOrder => query
                .OrderBy(p => p.DisplayOrder == null)
                .ThenBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Id),

            AdminProductListSort.Featured => query
                .OrderByDescending(p => p.IsFeatured)
                .ThenBy(p => p.DisplayOrder == null)
                .ThenBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Id),

            // Products without SKUs sort last (nulls last equivalent).
            AdminProductListSort.PriceAsc => query
                .OrderBy(p => !p.Skus.Any())
                .ThenBy(p => p.Skus.Any(s => s.IsActive)
                    ? p.Skus.Where(s => s.IsActive)
                        .Min(s => s.Price.Promotional != null
                            ? (s.Price.Promotional.Amount < s.Price.Regular.Amount
                                ? s.Price.Promotional.Amount
                                : s.Price.Regular.Amount)
                            : s.Price.Regular.Amount)
                    : p.Skus.Any()
                        ? p.Skus.Min(s => s.Price.Promotional != null
                            ? (s.Price.Promotional.Amount < s.Price.Regular.Amount
                                ? s.Price.Promotional.Amount
                                : s.Price.Regular.Amount)
                            : s.Price.Regular.Amount)
                        : 0m)
                .ThenBy(p => p.Id),

            AdminProductListSort.PriceDesc => query
                .OrderBy(p => !p.Skus.Any())
                .ThenByDescending(p => p.Skus.Any(s => s.IsActive)
                    ? p.Skus.Where(s => s.IsActive)
                        .Min(s => s.Price.Promotional != null
                            ? (s.Price.Promotional.Amount < s.Price.Regular.Amount
                                ? s.Price.Promotional.Amount
                                : s.Price.Regular.Amount)
                            : s.Price.Regular.Amount)
                    : p.Skus.Any()
                        ? p.Skus.Min(s => s.Price.Promotional != null
                            ? (s.Price.Promotional.Amount < s.Price.Regular.Amount
                                ? s.Price.Promotional.Amount
                                : s.Price.Regular.Amount)
                            : s.Price.Regular.Amount)
                        : 0m)
                .ThenBy(p => p.Id),

            // default / newest
            _ => query
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Id)
        };
}
