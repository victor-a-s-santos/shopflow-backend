using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Mappers;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Infrastructure.Repositories;

public sealed class ProductReadModel(CatalogDbContext db) : IProductReadModel
{
    public async Task<ProductDetailedDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await LoadDetailedProductQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return product is null ? null : MapToDetailedDto(product);
    }

    public async Task<ProductDetailedDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.Trim();
        var product = await LoadDetailedProductQuery()
            .FirstOrDefaultAsync(p => p.Slug.Value == normalized && p.IsActive, ct);

        return product is null ? null : MapToDetailedDto(product);
    }

    public async Task<PagedProductsDto> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var total = await db.Products.CountAsync(ct);

        var items = await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                Slug = p.Slug.Value,
                p.IsActive,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                p.HasSkus,
                BaseRegular = p.BasePrice.Regular.Amount,
                BasePromo = p.BasePrice.Promotional != null ? p.BasePrice.Promotional.Amount : (decimal?)null,
                Skus = p.Skus.Select(s => new
                {
                    s.IsActive,
                    Regular = s.Price.Regular.Amount,
                    Promo = s.Price.Promotional != null ? s.Price.Promotional.Amount : (decimal?)null,
                    SalesMode = s.SalesRule.SalesMode,
                    MinimumQuantity = s.SalesRule.MinimumQuantity,
                    QuantityStep = s.SalesRule.QuantityStep,
                    PackageSize = s.SalesRule.PackageSize,
                    PackageLabel = s.SalesRule.PackageLabel,
                    PackageDescription = s.SalesRule.PackageDescription,
                    QuantityUnitLabel = s.SalesRule.QuantityUnitLabel,
                    ShowTotalPieces = s.SalesRule.ShowTotalPieces
                }).ToList(),
                PrimaryImageUrl = p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault()
                    ?? p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync(ct);

        var dtos = items.Select(p =>
        {
            decimal? promo = null;
            decimal effective = 0;
            decimal regularPrice = p.BaseRegular;
            ProductSalesSummaryDto? salesSummary = null;

            if (!p.HasSkus)
            {
                promo = p.BasePromo;
                effective = promo.HasValue ? Math.Min(p.BaseRegular, promo.Value) : p.BaseRegular;
            }
            else
            {
                var actives = p.Skus.Where(s => s.IsActive).ToList();
                if (actives.Count != 0)
                {
                    regularPrice = actives.Min(s => s.Regular);
                    var promos = actives.Where(s => s.Promo.HasValue).Select(s => s.Promo!.Value);
                    promo = promos.Any() ? promos.Min() : null;
                    effective = promo.HasValue ? Math.Min(regularPrice, promo.Value) : regularPrice;

                    var skuInputs = actives.Select(s =>
                    {
                        var skuEffective = s.Promo.HasValue
                            ? Math.Min(s.Regular, s.Promo.Value)
                            : s.Regular;
                        return new ProductSalesSummaryFactory.SkuInput(
                            s.SalesMode,
                            s.MinimumQuantity,
                            s.QuantityStep,
                            s.PackageSize,
                            s.PackageLabel,
                            s.PackageDescription,
                            s.QuantityUnitLabel,
                            s.ShowTotalPieces,
                            skuEffective);
                    }).ToList();

                    salesSummary = ProductSalesSummaryFactory.FromActiveSkus(skuInputs);
                }
            }

            return new ProductDto(
                p.Id,
                p.Name,
                p.Slug,
                p.IsActive,
                p.HasSkus,
                regularPrice,
                promo,
                effective,
                p.CategoryId,
                p.CategoryName,
                p.PrimaryImageUrl,
                salesSummary);
        });

        return new PagedProductsDto(page, pageSize, total, dtos);
    }

    private IQueryable<Product> LoadDetailedProductQuery()
        => db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Skus)
            .ThenInclude(s => s.Attributes)
            .ThenInclude(a => a.AttributeDefinition)
            .Include(p => p.Skus)
            .ThenInclude(s => s.Attributes)
            .ThenInclude(a => a.AttributeValueDefinition);

    private static ProductDetailedDto MapToDetailedDto(Product product)
    {
        var effectivePrice = product.GetDisplayPrice(DateTimeOffset.UtcNow);

        var imageDtos = product.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, i.Url, i.SortOrder, i.IsPrimary))
            .ToList();

        return new ProductDetailedDto
        (
            product.Id,
            product.Name,
            product.Slug.Value,
            product.IsActive,
            product.CategoryId,
            product.Category?.Name,
            product.HasSkus,
            effectivePrice.Regular,
            effectivePrice.Promotional,
            effectivePrice.Effective,
            product.Skus.Select(SkuDtoMapper.FromEntity).ToList(),
            imageDtos
        );
    }
}
