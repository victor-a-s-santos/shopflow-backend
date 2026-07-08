using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Infrastructure.Seed;

public static class DemoClothingCatalogSeed
{
    private const string SeedProductsFolder = "seed-products";

    public const string InventoryReason = "Carga inicial demo catálogo roupas";

    public sealed record SeedResult(
        int ProductsCreated,
        int ProductsSkipped,
        int SkusCreated,
        int SkusSkipped,
        int ImagesCopied,
        int ImagesSkipped,
        int AttributeValuesAdded);

    public static async Task<SeedResult> SeedAsync(
        CatalogDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        IWebHostEnvironment webHostEnvironment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var options = configuration.GetSection(DemoCatalogSeedOptions.SectionName).Get<DemoCatalogSeedOptions>()
                      ?? new DemoCatalogSeedOptions();

        if (!options.Enabled)
        {
            logger.LogInformation("Demo clothing catalog seed is disabled (DemoCatalogSeed:Enabled=false).");
            return new SeedResult(0, 0, 0, 0, 0, 0, 0);
        }

        logger.LogInformation("Starting demo clothing catalog seed...");

        var attributeValuesAdded = await EnsureAttributeValuesAsync(db, logger, cancellationToken);

        var corDefinition = await db.AttributeDefinitions
            .Include(a => a.Values)
            .FirstAsync(a => a.Name == "Cor", cancellationToken);

        var tamanhoDefinition = await db.AttributeDefinitions
            .Include(a => a.Values)
            .FirstAsync(a => a.Name == "Tamanho", cancellationToken);

        var categories = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var categoriesByName = categories.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var uploadsRoot = ResolveUploadsRoot(configuration, webHostEnvironment);
        var publicBaseUrl = configuration["Uploads:PublicBaseUrl"]?.TrimEnd('/') ?? string.Empty;
        var seedAssetsDir = ResolveSeedAssetsDirectory(webHostEnvironment);
        var seedProductsDir = Path.Combine(uploadsRoot, SeedProductsFolder);
        Directory.CreateDirectory(seedProductsDir);

        ValidateSeedAssets(options, environment, seedAssetsDir, logger);

        var productsCreated = 0;
        var productsSkipped = 0;
        var skusCreated = 0;
        var skusSkipped = 0;
        var imagesCopied = 0;
        var imagesSkipped = 0;

        foreach (var definition in DemoClothingCatalogSeedData.Products)
        {
            if (!categoriesByName.TryGetValue(definition.CategoryName, out var categoryId))
            {
                logger.LogWarning(
                    "Demo seed: category {Category} not found; skipping product {Slug}.",
                    definition.CategoryName,
                    definition.Slug);
                continue;
            }

            var product = await db.Products
                .Include(p => p.Skus)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Slug.Value == definition.Slug, cancellationToken);

            if (product is null)
            {
                product = Product.CreateWithSkus(definition.Name, Slug.From(definition.Slug), categoryId);
                db.Products.Add(product);
                productsCreated++;
                logger.LogInformation("Demo seed: created product {Slug}.", definition.Slug);
            }
            else
            {
                productsSkipped++;
                logger.LogDebug("Demo seed: product {Slug} already exists.", definition.Slug);
            }

            var imageSortOrder = product.Images.Count;
            foreach (var color in definition.Colors)
            {
                var publicFileName = color.PublicFileName ?? SanitizePublicFileName(color.SourceFileName);
                var publicUrl = BuildPublicUrl(publicBaseUrl, publicFileName);
                var storagePath = $"{SeedProductsFolder}/{publicFileName}";

                if (options.CopyImages && seedAssetsDir is not null)
                {
                    var copied = TryCopySeedImage(
                        seedAssetsDir,
                        seedProductsDir,
                        color.SourceFileName,
                        publicFileName,
                        logger);

                    if (copied)
                        imagesCopied++;
                }

                if (product.Images.Any(i =>
                        string.Equals(i.Url, publicUrl, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(i.StoragePath, storagePath, StringComparison.OrdinalIgnoreCase)))
                {
                    imagesSkipped++;
                    continue;
                }

                product.AddImage(ProductImage.Create(
                    product.Id,
                    publicUrl,
                    storagePath,
                    imageSortOrder,
                    isPrimary: imageSortOrder == 0));

                imageSortOrder++;
            }

            var sizes = definition.LetterSizes ?? definition.NumericSizes
                        ?? throw new InvalidOperationException($"Product {definition.Slug} has no sizes.");

            foreach (var color in definition.Colors)
            {
                var colorValueId = ResolveAttributeValueId(corDefinition, color.ColorName);

                foreach (var size in sizes)
                {
                    var sizeValueId = ResolveAttributeValueId(tamanhoDefinition, size);
                    var skuCode = BuildSkuCode(definition.SkuBase, color.ColorName, size);

                    if (product.Skus.Any(s => string.Equals(s.Code, skuCode, StringComparison.OrdinalIgnoreCase))
                        || await db.Skus.AnyAsync(
                            s => s.ProductId == product.Id && s.Code == skuCode,
                            cancellationToken))
                    {
                        skusSkipped++;
                        continue;
                    }

                    var sku = Sku.Create(
                        product.Id,
                        skuCode,
                        Price.From(definition.RegularPrice, definition.PromotionalPrice),
                        [
                            SkuAttribute.FromGlobal(corDefinition.Id, colorValueId),
                            SkuAttribute.FromGlobal(tamanhoDefinition.Id, sizeValueId)
                        ],
                        active: true);

                    product.AddSku(sku);
                    skusCreated++;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Demo clothing catalog seed finished. Products created={ProductsCreated}, skipped={ProductsSkipped}, " +
            "SKUs created={SkusCreated}, skipped={SkusSkipped}, images copied={ImagesCopied}, skipped={ImagesSkipped}, " +
            "attribute values added={AttributeValuesAdded}.",
            productsCreated,
            productsSkipped,
            skusCreated,
            skusSkipped,
            imagesCopied,
            imagesSkipped,
            attributeValuesAdded);

        return new SeedResult(
            productsCreated,
            productsSkipped,
            skusCreated,
            skusSkipped,
            imagesCopied,
            imagesSkipped,
            attributeValuesAdded);
    }

    public static async Task<IReadOnlyList<Guid>> GetDemoSkuIdsAsync(
        CatalogDbContext db,
        CancellationToken cancellationToken = default)
    {
        return await db.Products
            .AsNoTracking()
            .Where(p => DemoClothingCatalogSeedData.DemoProductSlugs.Contains(p.Slug.Value))
            .SelectMany(p => p.Skus.Select(s => s.Id))
            .ToListAsync(cancellationToken);
    }

    private static async Task<int> EnsureAttributeValuesAsync(
        CatalogDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var added = 0;

        var corId = await db.AttributeDefinitions
            .AsNoTracking()
            .Where(a => a.Name == "Cor")
            .Select(a => a.Id)
            .FirstAsync(cancellationToken);

        var existingColorNames = await db.AttributeValueDefinitions
            .AsNoTracking()
            .Where(v => v.AttributeDefinitionId == corId)
            .Select(v => v.Name)
            .ToListAsync(cancellationToken);

        var existingColors = new HashSet<string>(existingColorNames, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, hex) in DemoClothingCatalogSeedData.RequiredColorHex)
        {
            if (existingColors.Contains(name))
                continue;

            await db.AttributeValueDefinitions.AddAsync(
                new AttributeValueDefinition(corId, name, hex),
                cancellationToken);
            added++;
            logger.LogInformation("Demo seed: added color value {Color}.", name);
        }

        var tamanhoId = await db.AttributeDefinitions
            .AsNoTracking()
            .Where(a => a.Name == "Tamanho")
            .Select(a => a.Id)
            .FirstAsync(cancellationToken);

        var existingSizeNames = await db.AttributeValueDefinitions
            .AsNoTracking()
            .Where(v => v.AttributeDefinitionId == tamanhoId)
            .Select(v => v.Name)
            .ToListAsync(cancellationToken);

        var existingSizes = new HashSet<string>(existingSizeNames, StringComparer.OrdinalIgnoreCase);

        foreach (var size in DemoClothingCatalogSeedData.RequiredSizes)
        {
            if (existingSizes.Contains(size))
                continue;

            await db.AttributeValueDefinitions.AddAsync(
                new AttributeValueDefinition(tamanhoId, size, null),
                cancellationToken);
            added++;
            logger.LogInformation("Demo seed: added size value {Size}.", size);
        }

        if (added > 0)
            await db.SaveChangesAsync(cancellationToken);

        db.ChangeTracker.Clear();
        return added;
    }

    private static Guid ResolveAttributeValueId(AttributeDefinition definition, string valueName)
    {
        var value = definition.Values.FirstOrDefault(v =>
                        string.Equals(v.Name, valueName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"Attribute value '{valueName}' not found for definition '{definition.Name}'.");

        return value.Id;
    }

    public static string BuildSkuCode(string skuBase, string colorName, string size)
        => $"{skuBase}-{NormalizeToken(colorName)}-{NormalizeToken(size)}";

    internal static string NormalizeToken(string value)
    {
        var normalized = RemoveDiacritics(value.Trim()).ToUpperInvariant();
        return string.Join('-', normalized.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));
    }

    internal static string SanitizePublicFileName(string sourceFileName)
    {
        var fileName = Path.GetFileName(sourceFileName);
        return fileName
            .ToLowerInvariant()
            .Replace(' ', '-');
    }

    private static string BuildPublicUrl(string publicBaseUrl, string publicFileName)
    {
        var relative = $"/uploads/{SeedProductsFolder}/{publicFileName}";
        return string.IsNullOrWhiteSpace(publicBaseUrl)
            ? relative
            : $"{publicBaseUrl}{relative}";
    }

    private static bool TryCopySeedImage(
        string seedAssetsDir,
        string seedProductsDir,
        string sourceFileName,
        string publicFileName,
        ILogger logger)
    {
        var sourcePath = Path.Combine(seedAssetsDir, sourceFileName);
        var destinationPath = Path.Combine(seedProductsDir, publicFileName);

        if (!File.Exists(sourcePath))
        {
            logger.LogWarning("Demo seed: source image not found at {Path}.", sourcePath);
            return false;
        }

        if (File.Exists(destinationPath))
            return false;

        File.Copy(sourcePath, destinationPath);
        logger.LogDebug("Demo seed: copied image {Source} -> {Destination}.", sourceFileName, publicFileName);
        return true;
    }

    private static void ValidateSeedAssets(
        DemoCatalogSeedOptions options,
        IHostEnvironment environment,
        string? seedAssetsDir,
        ILogger logger)
    {
        if (!options.CopyImages)
            return;

        var requiredFiles = DemoClothingCatalogSeedData.Products
            .SelectMany(p => p.Colors)
            .Select(c => c.SourceFileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (seedAssetsDir is null || !Directory.Exists(seedAssetsDir))
        {
            var message = "Demo seed assets directory not found. Expected apps/api/seed-assets/catalog-products/.";
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing") || environment.IsStaging())
                throw new DirectoryNotFoundException(message);

            logger.LogWarning("{Message}", message);
            return;
        }

        var missing = requiredFiles
            .Where(file => !File.Exists(Path.Combine(seedAssetsDir, file)))
            .ToList();

        foreach (var file in missing)
            logger.LogWarning("Demo seed: missing image asset {File}.", file);

        if (missing.Count > 0
            && (environment.IsDevelopment() || environment.IsEnvironment("Testing") || environment.IsStaging()))
        {
            throw new FileNotFoundException(
                $"Demo catalog seed enabled but {missing.Count} image(s) are missing in {seedAssetsDir}.");
        }
    }

    private static string ResolveUploadsRoot(IConfiguration configuration, IWebHostEnvironment env)
    {
        var configured = configuration["Uploads:RootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
    }

    internal static string? ResolveSeedAssetsDirectory(IWebHostEnvironment env)
    {
        var candidates = new[]
        {
            Path.Combine(env.ContentRootPath, "seed-assets", "catalog-products"),
            Path.Combine(env.ContentRootPath, "..", "..", "seed-assets", "catalog-products"),
            Path.Combine(AppContext.BaseDirectory, "seed-assets", "catalog-products"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "seed-assets", "catalog-products"),
            "/app/seed-assets/catalog-products"
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
