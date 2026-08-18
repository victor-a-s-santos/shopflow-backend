using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.Catalog.Application.CommandHandlers;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Application.Validations;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Infrastructure.Services.Storage;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

public sealed class ProductImageStorageKeysTests
{
    [Fact]
    public void Build_UsesPrefixProductIdImageIdAndSlug()
    {
        var productId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var imageId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var key = ProductImageStorageKeys.Build("products", productId, imageId, "Camiseta Básica", ".webp");

        key.Should().Be("products/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/11111111222233334444555555555555-camiseta-basica.webp");
    }

    [Fact]
    public void BuildSeedKey_IsStableForIdempotentSeed()
    {
        var key = ProductImageStorageKeys.BuildSeedKey("products", "camiseta-basica", "camiseta-basica-branca.png");
        key.Should().Be("products/seed/camiseta-basica/camiseta-basica-branca.png");
    }

    [Fact]
    public void BuildPublicUrl_R2_DoesNotInsertUploadsSegment()
    {
        var url = ProductImageStorageKeys.BuildPublicUrl(
            "https://assets-teste.vipassessoriadigital.com.br",
            "products/p/i.webp",
            prependUploadsSegment: false);

        url.Should().Be("https://assets-teste.vipassessoriadigital.com.br/products/p/i.webp");
    }

    [Fact]
    public void BuildPublicUrl_Local_PrependsUploads()
    {
        var url = ProductImageStorageKeys.BuildPublicUrl(
            "http://localhost:5127",
            "products/p/i.png",
            prependUploadsSegment: true);

        url.Should().Be("http://localhost:5127/uploads/products/p/i.png");
    }
}

public sealed class LocalFileObjectStorageServiceTests
{
    [Fact]
    public async Task Upload_WritesFileUnderUploadsRoot_AndBuildsPublicUrl()
    {
        var root = Path.Combine(Path.GetTempPath(), "shopflow-local-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var options = Options.Create(new StorageOptions
            {
                Provider = StorageOptions.ProviderLocal,
                Local = new LocalStorageOptions
                {
                    RootPath = root,
                    PublicBaseUrl = "http://localhost:5127"
                }
            });

            var sut = new LocalFileObjectStorageService(
                options,
                Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
                Mock.Of<Microsoft.Extensions.Hosting.IHostEnvironment>(),
                NullLogger<LocalFileObjectStorageService>.Instance);

            await using var content = new MemoryStream("png-bytes"u8.ToArray());
            var result = await sut.UploadAsync(
                new ObjectStorageUploadRequest("products/p/i.png", content, "image/png"),
                CancellationToken.None);

            result.PublicUrl.Should().Be("http://localhost:5127/uploads/products/p/i.png");
            result.ObjectKey.Should().Be("products/p/i.png");
            result.SizeBytes.Should().Be(9);
            File.Exists(Path.Combine(root, "products", "p", "i.png")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

public sealed class UploadProductImageValidationTests
{
    [Fact]
    public void Validator_RejectsEmptyFile()
    {
        var validator = new UploadProductImageCommandValidator();
        using var stream = new MemoryStream();
        var result = validator.TestValidate(new UploadProductImageCommand(
            Guid.NewGuid(), stream, "a.png", "image/png", 0));
        result.ShouldHaveValidationErrorFor(x => x.Length);
    }

    [Fact]
    public void Validator_RejectsFileLargerThan5Mb()
    {
        var validator = new UploadProductImageCommandValidator();
        using var stream = new MemoryStream();
        var result = validator.TestValidate(new UploadProductImageCommand(
            Guid.NewGuid(),
            stream,
            "a.png",
            "image/png",
            UploadProductImageCommandValidator.MaxBytes + 1));
        result.ShouldHaveValidationErrorFor(x => x.Length);
    }

    [Fact]
    public async Task Handler_RejectsInvalidMime()
    {
        var product = Product.CreateWithSkus("P", Slug.From("p"), null);
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var storage = new Mock<IImageStorage>();
        var sut = new UploadProductImageCommandHandler(
            repo.Object,
            storage.Object,
            Mock.Of<ICatalogUnitOfWork>());

        await using var stream = new MemoryStream("not-an-image"u8.ToArray());
        var act = () => sut.Handle(
            new UploadProductImageCommand(product.Id, stream, "x.gif", "image/gif", stream.Length),
            CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        storage.Verify(
            x => x.SaveAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handler_UploadsValidPng_AndPersistsPublicUrl()
    {
        var product = Product.CreateWithSkus("P", Slug.From("p"), null);
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        repo.Setup(x => x.AddImageAsync(It.IsAny<ProductImage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var imageId = Guid.NewGuid();
        var objectKey = $"products/{product.Id:D}/{imageId:N}-p.png";
        var storage = new Mock<IImageStorage>();
        storage.Setup(x => x.SaveAsync(
                product.Id,
                "p",
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                "image/png",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredImage(
                imageId,
                "https://assets-teste.vipassessoriadigital.com.br/" + objectKey,
                objectKey,
                "CloudflareR2",
                "image/png",
                12));

        var uow = new Mock<ICatalogUnitOfWork>();
        var sut = new UploadProductImageCommandHandler(repo.Object, storage.Object, uow.Object);

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        await using var stream = new MemoryStream(png);

        var dto = await sut.Handle(
            new UploadProductImageCommand(product.Id, stream, "foto.png", "image/png", png.Length),
            CancellationToken.None);

        dto.Id.Should().Be(imageId);
        dto.Url.Should().StartWith("https://assets-teste.vipassessoriadigital.com.br/");
        product.Images.Should().ContainSingle(i =>
            i.Id == imageId
            && i.StorageProvider == "CloudflareR2"
            && i.ObjectKey == objectKey
            && i.ContentType == "image/png"
            && i.SizeBytes == 12);
    }
}

public sealed class DeleteProductImageHandlerTests
{
    [Fact]
    public async Task Delete_RemovesDbRow_AndCallsStorageDelete()
    {
        var product = Product.CreateWithSkus("P", Slug.From("p"), null);
        var image = ProductImage.Create(
            product.Id,
            "https://assets-teste.vipassessoriadigital.com.br/products/p/i.png",
            "products/p/i.png",
            0,
            true,
            "CloudflareR2",
            contentType: "image/png",
            sizeBytes: 10);
        product.AddImage(image);

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var storage = new Mock<IImageStorage>();
        storage.Setup(x => x.TryDeleteAsync("products/p/i.png", "CloudflareR2", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uow = new Mock<ICatalogUnitOfWork>();
        var sut = new DeleteProductImageCommandHandler(
            repo.Object,
            uow.Object,
            storage.Object,
            NullLogger<DeleteProductImageCommandHandler>.Instance);

        await sut.Handle(new DeleteProductImageCommand(product.Id, image.Id), CancellationToken.None);

        product.Images.Should().BeEmpty();
        storage.Verify(
            x => x.TryDeleteAsync("products/p/i.png", "CloudflareR2", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
