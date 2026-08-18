using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Infrastructure;
using Vls.Shopflow.Catalog.Infrastructure.Repositories;
using Vls.Shopflow.Catalog.Infrastructure.Seed;
using Vls.Shopflow.Inventory.Infrastructure;
using Vls.Shopflow.Inventory.Infrastructure.Seed;

namespace Vls.Shopflow.Catalog.IntegrationTests;

[Collection("CatalogDbSequential")]
public sealed class DemoClothingCatalogSeedIntegrationTests
{
    private static readonly SemaphoreSlim MigrationGate = new(1, 1);
    private static bool _databasesMigrated;

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateCatalogContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static CatalogDbContext CreateCatalogContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new CatalogDbContext(options);
    }

    private static InventoryDbContext CreateInventoryContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new InventoryDbContext(options);
    }

    private static IConfiguration CreateSeedConfiguration(bool enabled = true, string publicBaseUrl = "http://localhost:5127")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoCatalogSeed:Enabled"] = enabled.ToString(),
                ["DemoCatalogSeed:CopyImages"] = "true",
                ["DemoCatalogSeed:CreateInventory"] = "true",
                ["DemoCatalogSeed:DefaultStockQuantity"] = "20",
                ["Uploads:PublicBaseUrl"] = publicBaseUrl
            })
            .Build();
    }

    private static TestHostEnvironment CreateHostEnvironment()
    {
        var apiRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return new TestHostEnvironment
        {
            ContentRootPath = Path.Combine(apiRoot, "ApiGateways", "Vls.Shopflow.HttpApi"),
            EnvironmentName = Environments.Development
        };
    }

    private static async Task EnsureMigratedAsync(CatalogDbContext catalogDb, InventoryDbContext inventoryDb)
    {
        await MigrationGate.WaitAsync();
        try
        {
            if (_databasesMigrated)
                return;

            if ((await catalogDb.Database.GetPendingMigrationsAsync()).Any())
                await catalogDb.Database.MigrateAsync();

            if ((await inventoryDb.Database.GetPendingMigrationsAsync()).Any())
            {
                try
                {
                    await inventoryDb.Database.MigrateAsync();
                }
                catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07")
                {
                    // Shared dev DB may already have inventory schema from API startup.
                }
            }

            _databasesMigrated = true;
        }
        finally
        {
            MigrationGate.Release();
        }
    }

    private static async Task RunDemoSeedAsync(
        CatalogDbContext catalogDb,
        InventoryDbContext inventoryDb,
        string publicBaseUrl = "http://localhost:5127")
    {
        var configuration = CreateSeedConfiguration(publicBaseUrl: publicBaseUrl);
        var environment = CreateHostEnvironment();
        var logger = NullLogger.Instance;

        await CatalogDbContextSeed.SeedAsync(catalogDb);
        await DemoClothingCatalogSeed.SeedAsync(
            catalogDb,
            configuration,
            environment,
            environment,
            logger);

        var skuIds = await DemoClothingCatalogSeed.GetDemoSkuIdsAsync(catalogDb);
        await DemoClothingInventorySeed.SeedAsync(
            inventoryDb,
            skuIds,
            20,
            DemoClothingCatalogSeed.InventoryReason,
            logger);
    }

    [Fact]
    public async Task SeedAsync_CreatesTenDemoProductsWithNinetyFourSkus()
    {
        if (!await CanConnectAsync())
            return;

        await using var catalogDb = CreateCatalogContext();
        await using var inventoryDb = CreateInventoryContext();
        await EnsureMigratedAsync(catalogDb, inventoryDb);

        await RunDemoSeedAsync(catalogDb, inventoryDb);

        var productCount = await catalogDb.Products
            .CountAsync(p => DemoClothingCatalogSeedData.DemoProductSlugs.Contains(p.Slug.Value));

        var skuCount = await catalogDb.Skus
            .CountAsync(s => DemoClothingCatalogSeedData.DemoProductSlugs.Contains(s.Product.Slug.Value));

        var imageCount = await catalogDb.ProductImages
            .CountAsync(i => DemoClothingCatalogSeedData.DemoProductSlugs.Contains(i.Product.Slug.Value));

        productCount.Should().Be(10);
        skuCount.Should().Be(94);
        imageCount.Should().Be(20);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        if (!await CanConnectAsync())
            return;

        await using var catalogDb = CreateCatalogContext();
        await using var inventoryDb = CreateInventoryContext();
        await EnsureMigratedAsync(catalogDb, inventoryDb);

        await RunDemoSeedAsync(catalogDb, inventoryDb);
        await RunDemoSeedAsync(catalogDb, inventoryDb);

        var productCount = await catalogDb.Products
            .CountAsync(p => DemoClothingCatalogSeedData.DemoProductSlugs.Contains(p.Slug.Value));

        var skuCount = await catalogDb.Skus
            .CountAsync(s => DemoClothingCatalogSeedData.DemoProductSlugs.Contains(s.Product.Slug.Value));

        var demoSkuIds = await DemoClothingCatalogSeed.GetDemoSkuIdsAsync(catalogDb);

        var inventoryCount = await inventoryDb.InventoryItems
            .CountAsync(i => demoSkuIds.Contains(i.SkuId));

        productCount.Should().Be(10);
        skuCount.Should().Be(94);
        inventoryCount.Should().Be(94);

        // Shared local DB may have reservations from other tests; seed is idempotent on item count,
        // not on exact on-hand totals after the initial load.
        var totalOnHand = await inventoryDb.InventoryItems
            .Where(i => demoSkuIds.Contains(i.SkuId))
            .SumAsync(i => i.QuantityOnHand);

        totalOnHand.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SeedAsync_WhenPublicBaseUrlChanges_DoesNotCrashAndKeepsImageCount()
    {
        if (!await CanConnectAsync())
            return;

        await using var catalogDb = CreateCatalogContext();
        await using var inventoryDb = CreateInventoryContext();
        await EnsureMigratedAsync(catalogDb, inventoryDb);

        await RunDemoSeedAsync(catalogDb, inventoryDb, publicBaseUrl: "http://localhost:5127");
        catalogDb.ChangeTracker.Clear();

        // Simulates redeploy with a new public API origin (common in teste/hml).
        await RunDemoSeedAsync(catalogDb, inventoryDb, publicBaseUrl: "https://api-teste.vipassessoriadigital.com.br");

        var imageCount = await catalogDb.ProductImages
            .CountAsync(i => DemoClothingCatalogSeedData.DemoProductSlugs.Contains(i.Product.Slug.Value));

        imageCount.Should().Be(20);

        var refreshed = await catalogDb.ProductImages
            .AsNoTracking()
            .Where(i => i.ObjectKey == "seed-products/camiseta-basica-branca.png")
            .Select(i => i.Url)
            .FirstOrDefaultAsync();

        refreshed.Should().Be("https://api-teste.vipassessoriadigital.com.br/uploads/seed-products/camiseta-basica-branca.png");
    }

    [Fact]
    public void FindExistingSeedImage_MatchesByFileNameEvenWhenAbsoluteUrlDiffers()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta-basica-algodao"), null);
        var existing = ProductImage.Create(
            product.Id,
            "http://localhost:5127/uploads/seed-products/camiseta-basica-branca.png",
            "seed-products/camiseta-basica-branca.png",
            0,
            true);
        product.AddImage(existing);

        var found = DemoClothingCatalogSeed.FindExistingSeedImage(
            product,
            publicFileName: "camiseta-basica-branca.png",
            publicUrl: "https://api-teste.vipassessoriadigital.com.br/uploads/seed-products/camiseta-basica-branca.png",
            objectKey: "seed-products/camiseta-basica-branca.png");

        found.Should().NotBeNull();
        found!.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task SeedAsync_ProductBySlug_HasImagesAndSkusWithAttributes()
    {
        if (!await CanConnectAsync())
            return;

        await using var catalogDb = CreateCatalogContext();
        await using var inventoryDb = CreateInventoryContext();
        await EnsureMigratedAsync(catalogDb, inventoryDb);

        await RunDemoSeedAsync(catalogDb, inventoryDb);

        var readModel = new ProductReadModel(catalogDb);
        var product = await readModel.GetBySlugAsync("camiseta-basica-algodao", CancellationToken.None);

        product.Should().NotBeNull();
        product!.Images.Should().HaveCount(2);
        product.Skus.Should().HaveCount(10);
        product.Skus.Should().OnlyContain(s =>
            s.Attributes.Any(a => a.DefinitionName == "Cor")
            && s.Attributes.Any(a => a.DefinitionName == "Tamanho"));
    }

    [Fact]
    public void BuildSkuCode_NormalizesColorAndSizeTokens()
    {
        DemoClothingCatalogSeed.BuildSkuCode("CAM-BAS", "Verde suave", "M")
            .Should().Be("CAM-BAS-VERDE-SUAVE-M");

        DemoClothingCatalogSeed.BuildSkuCode("CAL-JNS-MAS", "Azul jeans", "42")
            .Should().Be("CAL-JNS-MAS-AZUL-JEANS-42");
    }

    private sealed class TestHostEnvironment : IHostEnvironment, IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Shopflow.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
