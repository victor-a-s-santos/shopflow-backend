using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Services;
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
        IObjectStorageService? objectStorage = null,
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
        var publicBaseUrl = configuration["Storage:Local:PublicBaseUrl"]
                            ?? configuration["Uploads:PublicBaseUrl"]?.TrimEnd('/')
                            ?? string.Empty;
        var seedAssetsDir = ResolveSeedAssetsDirectory(webHostEnvironment);
        var seedProductsDir = Path.Combine(uploadsRoot, SeedProductsFolder);

        var useR2 = objectStorage is not null
                    && string.Equals(
                        objectStorage.ProviderName,
                        StorageOptions.ProviderCloudflareR2,
                        StringComparison.OrdinalIgnoreCase);
        var keyPrefix = configuration["Storage:R2:KeyPrefix"]
                        ?? configuration["R2Storage:ProductImagesPrefix"]
                        ?? "products";

        if (!useR2)
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

            // Detach previous product work so one failure cannot poison later products.
            db.ChangeTracker.Clear();

            var product = await db.Products
                .Include(p => p.Skus)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Slug.Value == definition.Slug, cancellationToken);

            var isNewProduct = product is null;
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
            var productAlreadyHasImages = product.Images.Count > 0;

            var r2PublicBaseUrl = configuration["Storage:R2:PublicBaseUrl"]
                                  ?? configuration["R2Storage:PublicBaseUrl"]
                                  ?? string.Empty;

            foreach (var color in definition.Colors)
            {
                var publicFileName = color.PublicFileName ?? SanitizePublicFileName(color.SourceFileName);

                if (useR2 && objectStorage is not null)
                {
                    var targetKey = ProductImageStorageKeys.BuildSeedKey(
                        keyPrefix,
                        definition.Slug,
                        publicFileName);
                    var targetUrl = objectStorage.BuildPublicUrl(targetKey);
                    var existingImage = FindExistingSeedImage(product, publicFileName, targetUrl, targetKey);

                    if (existingImage is not null)
                    {
                        var needsUpload = DemoSeedR2MigrationRules.NeedsR2Upload(
                            existingImage,
                            targetKey,
                            r2PublicBaseUrl);

                        if (!needsUpload)
                        {
                            var objectExists = await objectStorage.ExistsAsync(
                                existingImage.ObjectKey ?? targetKey,
                                cancellationToken);
                            if (objectExists)
                            {
                                imagesSkipped++;
                                continue;
                            }

                            needsUpload = true;
                            logger.LogWarning(
                                "Demo seed: CloudflareR2 row {ImageId} missing object {Key}; will re-upload.",
                                existingImage.Id,
                                existingImage.ObjectKey ?? targetKey);
                        }

                        if (!options.CopyImages || seedAssetsDir is null)
                        {
                            logger.LogWarning(
                                "Demo seed: cannot migrate image {File} — CopyImages disabled or assets missing.",
                                publicFileName);
                            continue;
                        }

                        var sourcePath = Path.Combine(seedAssetsDir, color.SourceFileName);
                        if (!File.Exists(sourcePath))
                        {
                            logger.LogWarning("Demo seed: missing source image {File}.", color.SourceFileName);
                            continue;
                        }

                        try
                        {
                            var mime = GuessContentType(publicFileName);
                            var objectAlreadyThere = await objectStorage.ExistsAsync(targetKey, cancellationToken);
                            ObjectStorageUploadResult uploaded;
                            if (objectAlreadyThere)
                            {
                                uploaded = new ObjectStorageUploadResult(
                                    targetKey,
                                    targetUrl,
                                    mime,
                                    new FileInfo(sourcePath).Length);
                                logger.LogInformation(
                                    "Demo seed: object {Key} already in R2; updating DB metadata only.",
                                    targetKey);
                            }
                            else
                            {
                                await using var fs = new FileStream(
                                    sourcePath,
                                    FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.Read,
                                    65536,
                                    useAsync: true);
                                uploaded = await objectStorage.UploadAsync(
                                    new ObjectStorageUploadRequest(
                                        targetKey,
                                        fs,
                                        mime,
                                        R2StorageOptions.ImageCacheControl),
                                    cancellationToken);
                            }

                            // Persist only after successful upload / confirmed object.
                            existingImage.MarkMigratedToObjectStorage(
                                uploaded.PublicUrl,
                                uploaded.ObjectKey,
                                StorageOptions.ProviderCloudflareR2,
                                uploaded.ContentType,
                                uploaded.SizeBytes);
                            await db.SaveChangesAsync(cancellationToken);
                            imagesCopied++;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                ex,
                                "Demo seed: R2 upload failed for {File}; leaving DB row unchanged.",
                                publicFileName);
                            db.ChangeTracker.Clear();
                            product = await db.Products
                                .Include(p => p.Skus)
                                .Include(p => p.Images)
                                .FirstAsync(p => p.Id == product.Id, cancellationToken);
                        }

                        continue;
                    }

                    // New image row — upload first, then insert.
                    string? contentType = null;
                    long? sizeBytes = null;
                    var publicUrl = targetUrl;
                    var blobReady = false;

                    if (options.CopyImages && seedAssetsDir is not null)
                    {
                        var sourcePath = Path.Combine(seedAssetsDir, color.SourceFileName);
                        if (!File.Exists(sourcePath))
                        {
                            logger.LogWarning("Demo seed: missing source image {File}.", color.SourceFileName);
                        }
                        else
                        {
                            try
                            {
                                var mime = GuessContentType(publicFileName);
                                if (await objectStorage.ExistsAsync(targetKey, cancellationToken))
                                {
                                    publicUrl = targetUrl;
                                    contentType = mime;
                                    sizeBytes = new FileInfo(sourcePath).Length;
                                    blobReady = true;
                                }
                                else
                                {
                                    await using var fs = new FileStream(
                                        sourcePath,
                                        FileMode.Open,
                                        FileAccess.Read,
                                        FileShare.Read,
                                        65536,
                                        useAsync: true);
                                    var uploaded = await objectStorage.UploadAsync(
                                        new ObjectStorageUploadRequest(
                                            targetKey,
                                            fs,
                                            mime,
                                            R2StorageOptions.ImageCacheControl),
                                        cancellationToken);
                                    publicUrl = uploaded.PublicUrl;
                                    contentType = uploaded.ContentType;
                                    sizeBytes = uploaded.SizeBytes;
                                    blobReady = true;
                                }

                                imagesCopied++;
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(
                                    ex,
                                    "Demo seed: R2 upload failed for new image {File}; skipping insert.",
                                    publicFileName);
                                continue;
                            }
                        }
                    }

                    if (!blobReady)
                    {
                        // No blob uploaded — do not insert a dangling R2 URL row.
                        logger.LogWarning(
                            "Demo seed: skipping insert for {File} without successful R2 upload.",
                            publicFileName);
                        continue;
                    }

                    var image = ProductImage.Create(
                        product.Id,
                        publicUrl,
                        targetKey,
                        sortOrder: imageSortOrder,
                        isPrimary: !productAlreadyHasImages && imageSortOrder == 0,
                        storageProvider: StorageOptions.ProviderCloudflareR2,
                        contentType: contentType,
                        sizeBytes: sizeBytes);

                    if (!productAlreadyHasImages)
                    {
                        product.AddImage(image);
                        imageSortOrder = product.Images.Count;
                    }
                    else
                    {
                        db.ProductImages.Add(image);
                        imageSortOrder++;
                    }

                    continue;
                }

                // Local provider path
                var objectKey = $"{SeedProductsFolder}/{publicFileName}";
                var localPublicUrl = BuildPublicUrl(publicBaseUrl, publicFileName);
                var existingLocal = FindExistingSeedImage(product, publicFileName, localPublicUrl, objectKey);
                if (existingLocal is not null)
                {
                    if (!string.Equals(existingLocal.Url, localPublicUrl, StringComparison.Ordinal))
                    {
                        await db.ProductImages
                            .Where(i => i.Id == existingLocal.Id)
                            .ExecuteUpdateAsync(
                                setters => setters.SetProperty(i => i.Url, localPublicUrl),
                                cancellationToken);
                    }

                    imagesSkipped++;
                    continue;
                }

                string? localContentType = null;
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
                    localContentType = GuessContentType(publicFileName);
                }

                var localImage = ProductImage.Create(
                    product.Id,
                    localPublicUrl,
                    objectKey,
                    sortOrder: imageSortOrder,
                    isPrimary: !productAlreadyHasImages && imageSortOrder == 0,
                    storageProvider: StorageOptions.ProviderLocal,
                    contentType: localContentType,
                    sizeBytes: null);

                if (!productAlreadyHasImages)
                {
                    product.AddImage(localImage);
                    imageSortOrder = product.Images.Count;
                }
                else
                {
                    db.ProductImages.Add(localImage);
                    imageSortOrder++;
                }
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

                    if (product.Skus.Any(s => string.Equals(s.Code, skuCode, StringComparison.OrdinalIgnoreCase)))
                    {
                        skusSkipped++;
                        continue;
                    }

                    var skuExistsInDb = !isNewProduct && await db.Skus.AnyAsync(
                        s => s.ProductId == product.Id && s.Code == skuCode,
                        cancellationToken);
                    if (skuExistsInDb)
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

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Never block API startup because of demo seed races / stale image rows.
                logger.LogWarning(
                    ex,
                    "Demo seed: concurrency conflict while seeding product {Slug}; skipping product and continuing.",
                    definition.Slug);
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(
                    ex,
                    "Demo seed: database update failed while seeding product {Slug}; skipping product and continuing.",
                    definition.Slug);
                db.ChangeTracker.Clear();
            }
        }

        db.ChangeTracker.Clear();

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

    /// <summary>
    /// Matches an existing seed image by exact URL/storage path or by file name suffix,
    /// so PublicBaseUrl changes do not create duplicate rows or mutate existing ones incorrectly.
    /// </summary>
    internal static ProductImage? FindExistingSeedImage(
        Product product,
        string publicFileName,
        string publicUrl,
        string objectKey)
    {
        return product.Images.FirstOrDefault(i =>
            string.Equals(i.Url, publicUrl, StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.ObjectKey, objectKey, StringComparison.OrdinalIgnoreCase)
            || ObjectKeyEndsWithFileName(i.ObjectKey, publicFileName)
            || UrlContainsFileName(i.Url, publicFileName));
    }

    internal static bool ObjectKeyEndsWithFileName(string? objectKey, string publicFileName)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return false;

        return objectKey.EndsWith(publicFileName, StringComparison.OrdinalIgnoreCase)
               || objectKey.EndsWith("/" + publicFileName, StringComparison.OrdinalIgnoreCase)
               || objectKey.EndsWith("\\" + publicFileName, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool UrlContainsFileName(string? url, string publicFileName)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return url.Contains("/" + publicFileName, StringComparison.OrdinalIgnoreCase)
               || url.EndsWith(publicFileName, StringComparison.OrdinalIgnoreCase);
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
        var configured = configuration["Storage:Local:RootPath"]
                         ?? configuration["Uploads:RootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
    }

    private static string GuessContentType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

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
