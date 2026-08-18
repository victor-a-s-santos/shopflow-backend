using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.CommandHandlers;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Validations;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

public sealed class ProductDescriptionAndStatusContractTests
{
    [Fact]
    public void CreateWithSkus_PersistsDescriptionAndIsActiveFalse()
    {
        var product = Product.CreateWithSkus(
            "Camiseta",
            Slug.From("camiseta"),
            null,
            description: "  Algodão premium  ",
            isActive: false);

        product.Description.Should().Be("Algodão premium");
        product.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreateWithSkus_EmptyDescriptionBecomesNull_DefaultActive()
    {
        var product = Product.CreateWithSkus(
            "Camiseta",
            Slug.From("camiseta"),
            null,
            description: "   ");

        product.Description.Should().BeNull();
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ChangeDescription_ClearsOnEmpty()
    {
        var product = Product.CreateWithSkus(
            "Camiseta",
            Slug.From("camiseta"),
            null,
            description: "Texto");

        product.ChangeDescription("  ");
        product.Description.Should().BeNull();
    }

    [Fact]
    public async Task CreateHandler_PersistsDescriptionAndExplicitInactive()
    {
        Product? saved = null;
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((p, _) => saved = p)
            .Returns(Task.CompletedTask);

        var slugService = new Mock<ISlugService>();
        slugService.Setup(x => x.EnsureUniqueAsync(It.IsAny<Slug>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Slug s, CancellationToken _) => s);

        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateVariantProductCommandHandler(repo.Object, slugService.Object, uow.Object);
        await handler.Handle(
            new CreateVariantProductCommand(
                "Camiseta",
                "camiseta",
                null,
                Description: "Confortável",
                IsActive: false),
            CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Description.Should().Be("Confortável");
        saved.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateHandler_MissingIsActive_DefaultsTrue()
    {
        Product? saved = null;
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((p, _) => saved = p)
            .Returns(Task.CompletedTask);

        var slugService = new Mock<ISlugService>();
        slugService.Setup(x => x.EnsureUniqueAsync(It.IsAny<Slug>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Slug s, CancellationToken _) => s);

        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateVariantProductCommandHandler(repo.Object, slugService.Object, uow.Object);
        await handler.Handle(
            new CreateVariantProductCommand("Camiseta", "camiseta", null),
            CancellationToken.None);

        saved!.IsActive.Should().BeTrue();
        saved.Description.Should().BeNull();
    }

    [Fact]
    public async Task UpdateHandler_ChangesDescriptionAndIsActiveFalse()
    {
        var product = Product.CreateWithSkus(
            "Camiseta",
            Slug.From("camiseta"),
            null,
            description: "Antiga",
            isActive: true);

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductCommandHandler(repo.Object, Mock.Of<ISlugService>(), uow.Object);
        await handler.Handle(
            new UpdateProductCommand(
                product.Id,
                "Camiseta",
                null,
                null,
                IsActive: false,
                Description: "Nova descrição",
                UpdateDescription: true),
            CancellationToken.None);

        product.Description.Should().Be("Nova descrição");
        product.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateHandler_ClearsDescription_WhenEmptySent()
    {
        var product = Product.CreateWithSkus(
            "Camiseta",
            Slug.From("camiseta"),
            null,
            description: "Antiga");

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductCommandHandler(repo.Object, Mock.Of<ISlugService>(), uow.Object);
        await handler.Handle(
            new UpdateProductCommand(
                product.Id,
                "Camiseta",
                null,
                null,
                true,
                Description: "",
                UpdateDescription: true),
            CancellationToken.None);

        product.Description.Should().BeNull();
    }

    [Fact]
    public async Task UpdateHandler_PreservesDescription_WhenNotFlagged()
    {
        var product = Product.CreateWithSkus(
            "Camiseta",
            Slug.From("camiseta"),
            null,
            description: "Manter",
            isActive: true);
        product.ChangeDisplaySettings(true, 2);

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductCommandHandler(repo.Object, Mock.Of<ISlugService>(), uow.Object);
        await handler.Handle(
            new UpdateProductCommand(product.Id, "Camiseta Nova", null, null, IsActive: false),
            CancellationToken.None);

        product.Name.Should().Be("Camiseta Nova");
        product.IsActive.Should().BeFalse();
        product.Description.Should().Be("Manter");
        product.IsFeatured.Should().BeTrue();
        product.DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task UpdateHandler_IsActiveFalse_IsNotIgnored()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta"), null);
        product.IsActive.Should().BeTrue();

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductCommandHandler(repo.Object, Mock.Of<ISlugService>(), uow.Object);
        await handler.Handle(
            new UpdateProductCommand(product.Id, "Camiseta", null, null, IsActive: false),
            CancellationToken.None);

        product.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreateValidator_RejectsDescriptionTooLong()
    {
        var result = new CreateVariantProductValidator().TestValidate(
            new CreateVariantProductCommand(
                "Camiseta",
                "camiseta",
                null,
                Description: new string('a', Product.MaxDescriptionLength + 1)));

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void UpdateValidator_RejectsDescriptionTooLong_WhenUpdating()
    {
        var result = new UpdateProductCommandValidator().TestValidate(
            new UpdateProductCommand(
                Guid.NewGuid(),
                "Camiseta",
                null,
                null,
                true,
                Description: new string('a', Product.MaxDescriptionLength + 1),
                UpdateDescription: true));

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }
}
