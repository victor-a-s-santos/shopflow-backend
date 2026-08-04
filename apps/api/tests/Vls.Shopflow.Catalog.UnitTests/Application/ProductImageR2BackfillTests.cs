using FluentAssertions;
using Moq;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Services.ProductImageR2Backfill;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

public sealed class ProductImageR2BackfillTests
{
    private static ProductImageBackfillOptions BaseOptions(
        bool execute = false,
        bool enabled = false,
        string environment = "Testing",
        string? confirm = null,
        string? connectionString = "Host=localhost;Database=shopflow_test")
        => new(
            EnvironmentName: environment,
            SourceRoot: Path.GetTempPath(),
            Execute: execute,
            ConfirmPhrase: confirm,
            BackfillFlagEnabled: enabled,
            StorageProvider: StorageOptions.ProviderCloudflareR2,
            R2Bucket: "shopflow-products-test",
            R2PublicBaseUrl: "https://assets-teste.vipassessoriadigital.com.br",
            KeyPrefix: "products",
            ConnectionString: connectionString,
            ReportPath: null);

    [Fact]
    public async Task Aborts_InProduction()
    {
        var runner = new ProductImageR2BackfillRunner(
            Mock.Of<IProductImageBackfillStore>(),
            Mock.Of<IObjectStorageService>());

        var act = () => runner.RunAsync(BaseOptions(environment: "Production"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Production*");
    }

    [Fact]
    public async Task Aborts_Execute_WithoutEnabledFlag()
    {
        var root = CreateTempSourceRoot();
        try
        {
            var runner = new ProductImageR2BackfillRunner(
                Mock.Of<IProductImageBackfillStore>(s =>
                    s.LoadAllAsync(It.IsAny<CancellationToken>())
                     == Task.FromResult<IReadOnlyList<ProductImageBackfillRow>>(
                         Array.Empty<ProductImageBackfillRow>())),
                Mock.Of<IObjectStorageService>());

            var act = () => runner.RunAsync(BaseOptions(execute: true, enabled: false) with
            {
                SourceRoot = root,
                ConfirmPhrase = R2ImageBackfillOptions.ConfirmPhrase
            });

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*R2ImageBackfill:Enabled*");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Aborts_Execute_WithoutConfirmPhrase()
    {
        var root = CreateTempSourceRoot();
        try
        {
            var runner = new ProductImageR2BackfillRunner(
                Mock.Of<IProductImageBackfillStore>(s =>
                    s.LoadAllAsync(It.IsAny<CancellationToken>())
                     == Task.FromResult<IReadOnlyList<ProductImageBackfillRow>>(
                         Array.Empty<ProductImageBackfillRow>())),
                Mock.Of<IObjectStorageService>());

            var act = () => runner.RunAsync(BaseOptions(execute: true, enabled: true) with
            {
                SourceRoot = root,
                ConfirmPhrase = "WRONG"
            });

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*confirm*");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task DryRun_DoesNotCallUpload_OrPersist()
    {
        var root = CreateTempSourceRoot();
        var relative = "products/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/img.png";
        var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllBytesAsync(absolute, [0x89, 0x50, 0x4E, 0x47]);

        var imageId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var productId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var store = new Mock<IProductImageBackfillStore>();
        store.Setup(s => s.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProductImageBackfillRow(
                    imageId,
                    productId,
                    "camiseta",
                    "http://localhost:5127/uploads/" + relative,
                    relative,
                    "Local",
                    "image/png",
                    4)
            ]);

        var storage = new Mock<IObjectStorageService>();
        var runner = new ProductImageR2BackfillRunner(store.Object, storage.Object);

        var report = await runner.RunAsync(BaseOptions() with { SourceRoot = root, Execute = false });

        report.Mode.Should().Be("dry-run");
        report.Eligible.Should().Be(1);
        report.Uploaded.Should().Be(0);
        storage.Verify(
            s => s.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        store.Verify(
            s => s.PersistMigrationAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ignores_AlreadyOnR2()
    {
        var root = CreateTempSourceRoot();
        var store = new Mock<IProductImageBackfillStore>();
        store.Setup(s => s.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProductImageBackfillRow(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "p",
                    "https://assets-teste.example/products/x.png",
                    "products/x.png",
                    StorageOptions.ProviderCloudflareR2,
                    "image/png",
                    10)
            ]);

        var runner = new ProductImageR2BackfillRunner(store.Object, Mock.Of<IObjectStorageService>());
        var report = await runner.RunAsync(BaseOptions() with { SourceRoot = root });

        report.AlreadyOnR2.Should().Be(1);
        report.Eligible.Should().Be(0);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Ignores_MissingLocalFile()
    {
        var root = CreateTempSourceRoot();
        var store = new Mock<IProductImageBackfillStore>();
        store.Setup(s => s.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProductImageBackfillRow(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "p",
                    "http://localhost/uploads/products/missing.png",
                    "products/missing.png",
                    "Local",
                    null,
                    null)
            ]);

        var runner = new ProductImageR2BackfillRunner(store.Object, Mock.Of<IObjectStorageService>());
        var report = await runner.RunAsync(BaseOptions() with { SourceRoot = root });

        report.Eligible.Should().Be(0);
        report.SkippedItems.Should().Contain(s => s.Reason == ProductImageBackfillSkipReason.MissingLocalFile);
        Directory.Delete(root, true);
    }

    [Fact]
    public void Builds_ObjectKey_WithExistingImageIdAndSlug()
    {
        var productId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var imageId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var key = ProductImageBackfillSelector.BuildPlannedObjectKey(
            "products",
            productId,
            imageId,
            "camiseta-basica",
            "products/old/file.png");

        key.Should().Be("products/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/11111111222233334444555555555555-camiseta-basica.png");
    }

    [Fact]
    public async Task Execute_UploadOk_UpdatesDb_DoesNotDeleteLocalFile()
    {
        var root = CreateTempSourceRoot();
        var relative = "seed-products/camiseta.png";
        var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllBytesAsync(absolute, [1, 2, 3, 4]);

        var imageId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var store = new Mock<IProductImageBackfillStore>();
        store.Setup(s => s.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProductImageBackfillRow(
                    imageId,
                    productId,
                    "camiseta-basica",
                    "http://localhost:5127/uploads/" + relative,
                    relative,
                    null,
                    null,
                    null)
            ]);
        store.Setup(s => s.PersistMigrationAsync(
                imageId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                StorageOptions.ProviderCloudflareR2,
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storage = new Mock<IObjectStorageService>();
        storage.Setup(s => s.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ObjectStorageUploadRequest req, CancellationToken _) =>
                new ObjectStorageUploadResult(
                    req.ObjectKey,
                    "https://assets-teste.vipassessoriadigital.com.br/" + req.ObjectKey,
                    req.ContentType,
                    4));

        var runner = new ProductImageR2BackfillRunner(store.Object, storage.Object);
        var report = await runner.RunAsync(BaseOptions(execute: true, enabled: true) with
        {
            SourceRoot = root,
            ConfirmPhrase = R2ImageBackfillOptions.ConfirmPhrase
        });

        report.Uploaded.Should().Be(1);
        report.Errors.Should().Be(0);
        File.Exists(absolute).Should().BeTrue();
        store.VerifyAll();
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Execute_UploadFails_DoesNotUpdateDb()
    {
        var root = CreateTempSourceRoot();
        var relative = "products/p/a.png";
        var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllBytesAsync(absolute, [1, 2, 3]);

        var imageId = Guid.NewGuid();
        var store = new Mock<IProductImageBackfillStore>();
        store.Setup(s => s.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProductImageBackfillRow(
                    imageId,
                    Guid.NewGuid(),
                    "p",
                    "http://localhost/uploads/" + relative,
                    relative,
                    "Local",
                    "image/png",
                    3)
            ]);

        var storage = new Mock<IObjectStorageService>();
        storage.Setup(s => s.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("R2 down"));

        var runner = new ProductImageR2BackfillRunner(store.Object, storage.Object);
        var report = await runner.RunAsync(BaseOptions(execute: true, enabled: true) with
        {
            SourceRoot = root,
            ConfirmPhrase = R2ImageBackfillOptions.ConfirmPhrase
        });

        report.Errors.Should().Be(1);
        report.Uploaded.Should().Be(0);
        store.Verify(
            s => s.PersistMigrationAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Directory.Delete(root, true);
    }

    [Fact]
    public void Report_DoesNotContainSecrets()
    {
        var report = new ProductImageBackfillReport(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "dry-run",
            "Testing",
            "/uploads",
            "shopflow-products-test",
            "https://assets-teste.vipassessoriadigital.com.br",
            1,
            0,
            0,
            0,
            0,
            0,
            [],
            [],
            []);

        var md = ProductImageBackfillReportWriter.FormatMarkdown(report);
        md.Should().NotContain("SecretAccessKey");
        md.Should().NotContain("AccessKeyId=");
        ProductImageBackfillReportWriter.AssertNoSecrets(md);
    }

    [Fact]
    public void Guards_DetectProductionConnectionString()
    {
        ProductImageBackfillGuards.LooksLikeProductionConnectionString(
                "Host=db;Database=shopflow_prod;Username=u;Password=p")
            .Should().BeTrue();

        ProductImageBackfillGuards.LooksLikeProductionConnectionString(
                "Host=db;Database=shopflow_test;Username=u;Password=p")
            .Should().BeFalse();
    }

    [Fact]
    public void MarkMigratedToObjectStorage_UpdatesFields()
    {
        var image = ProductImage.Create(
            Guid.NewGuid(),
            "http://localhost/uploads/a.png",
            "a.png",
            0,
            true,
            "Local");

        image.MarkMigratedToObjectStorage(
            "https://assets-teste.example/products/x.png",
            "products/x.png",
            StorageOptions.ProviderCloudflareR2,
            "image/png",
            99);

        image.Url.Should().Be("https://assets-teste.example/products/x.png");
        image.ObjectKey.Should().Be("products/x.png");
        image.StorageProvider.Should().Be("CloudflareR2");
        image.ContentType.Should().Be("image/png");
        image.SizeBytes.Should().Be(99);
    }

    private static string CreateTempSourceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "shopflow-backfill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
