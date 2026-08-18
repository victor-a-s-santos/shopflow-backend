using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Infrastructure;
using Vls.Shopflow.Catalog.Infrastructure.Repositories;

namespace Vls.Shopflow.Catalog.IntegrationTests;

public sealed class ProductReadModelTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static CatalogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new CatalogDbContext(options);
    }

    [Fact]
    public async Task GetByIdAndSlug_ReturnVariantProductWithSku()
    {
        if (!await CanConnectAsync())
            return;

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var slug = Slug.From($"test-product-{Guid.NewGuid():N}"[..24]);
        var product = Product.CreateWithSkus(
            "Produto Teste",
            slug,
            null,
            description: "Descrição de teste");
        var sku = Sku.Create(
            product.Id,
            "SKU-TEST",
            Price.From(120m, 99m),
            [SkuAttribute.FromCustom("Tamanho", "M")],
            active: true);
        product.AddSku(sku);

        setup.Products.Add(product);
        await setup.SaveChangesAsync();

        var readModel = new ProductReadModel(setup);

        var byId = await readModel.GetByIdAsync(product.Id, CancellationToken.None);
        var bySlug = await readModel.GetBySlugAsync(slug.Value, CancellationToken.None);

        byId.Should().NotBeNull();
        byId!.Description.Should().Be("Descrição de teste");
        byId.IsActive.Should().BeTrue();
        byId.Skus.Should().ContainSingle(s =>
            s.Code == "SKU-TEST" && s.RegularPrice == 120m && s.PromotionalPrice == 99m);

        bySlug.Should().NotBeNull();
        bySlug!.Slug.Should().Be(slug.Value);
        bySlug.Description.Should().Be("Descrição de teste");
        bySlug.Skus.Should().ContainSingle(s => s.Code == "SKU-TEST");
    }

    [Fact]
    public async Task GetBySlug_InactiveProduct_ReturnsNull()
    {
        if (!await CanConnectAsync())
            return;

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var slug = Slug.From($"inactive-{Guid.NewGuid():N}"[..20]);
        var product = Product.CreateWithSkus("Inativo", slug, null);
        product.Deactivate();

        setup.Products.Add(product);
        await setup.SaveChangesAsync();

        var readModel = new ProductReadModel(setup);
        var result = await readModel.GetBySlugAsync(slug.Value, CancellationToken.None);

        result.Should().BeNull();
    }
}
