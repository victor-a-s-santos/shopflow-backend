using MediatR;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.IdentityAccess.Domain.Constants;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder group)
    {
       var cat = group.MapGroup("/catalog").WithTags("Catalog");

        // -------------------------------------------------------
        // ATTRIBUTE DEFINITIONS (Globais)
        // -------------------------------------------------------

        cat.MapGet("/attributes", async (ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetAllAttributeDefinitionsQuery(), ct);
            return Results.Ok(dto);
        });
        
        cat.MapGet("/categories", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAllCategoriesQuery(), ct);
            return Results.Ok(result);
        });

        // -------------------------------------------------------
        // CREATE VARIANT PRODUCT
        // -------------------------------------------------------

        cat.MapPost("/products/variant",
            async (ISender sender, CreateVariantProductRequest req, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateVariantProductCommand(
                req.Name,
                req.Slug,
                req.CategoryId
            ), ct);

            return Results.Created($"/api/catalog/products/{id}", new { id });
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        // -------------------------------------------------------
        // ADD SKU (VARIANT)
        // -------------------------------------------------------

        cat.MapPost("/products/{productId:guid}/variants",
            async (ISender sender, Guid productId, AddVariantRequest req, CancellationToken ct) =>
        {
            var skuId = await sender.Send(new AddSkuCommand(
                productId,
                req.Code,
                req.RegularPrice,
                req.PromotionalPrice,
                req.Attributes,
                req.Active,
                req.SalesRule
            ), ct);

            return Results.Created($"/api/catalog/products/{productId}/variants/{skuId}", new { skuId });
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        // -------------------------------------------------------
        // ACTIVATE PRODUCT
        // -------------------------------------------------------

        cat.MapPost("/products/{id:guid}/activate",
            async (Guid id, ISender mediator) =>
            {
                await mediator.Send(new ActivateProductCommand(id));
                return Results.NoContent();
            })
        .RequireAuthorization(AuthPolicies.Backoffice);

        // -------------------------------------------------------
        // DEACTIVATE PRODUCT
        // -------------------------------------------------------

        cat.MapPost("/products/{id:guid}/deactivate",
            async (Guid id, ISender mediator) =>
            {
                await mediator.Send(new DeactivateProductCommand(id));
                return Results.NoContent();
            })
        .RequireAuthorization(AuthPolicies.Backoffice);

        // -------------------------------------------------------
        // UPDATE PRODUCT
        // -------------------------------------------------------

        cat.MapPut("/products/{id:guid}",
            async (ISender sender, Guid id, UpdateProductRequest req, CancellationToken ct) =>
            {
                await sender.Send(new UpdateProductCommand(
                    id,
                    req.Name,
                    req.Slug,
                    req.CategoryId,
                    req.IsActive
                ), ct);
                return Results.NoContent();
            })
        .RequireAuthorization(AuthPolicies.Backoffice);

        // -------------------------------------------------------
        // DELETE PRODUCT
        // -------------------------------------------------------

        cat.MapDelete("/products/{id:guid}",
            async (ISender sender, Guid id, CancellationToken ct) =>
            {
                await sender.Send(new DeleteProductCommand(id), ct);
                return Results.NoContent();
            })
        .RequireAuthorization(AuthPolicies.Backoffice);

        // -------------------------------------------------------
        // UPDATE SKU (VARIANT)
        // -------------------------------------------------------

        cat.MapPut("/products/{productId:guid}/variants/{skuId:guid}",
            async (ISender sender, Guid productId, Guid skuId, UpdateVariantRequest req, CancellationToken ct) =>
            {
                await sender.Send(new UpdateSkuCommand(
                    productId,
                    skuId,
                    req.Code,
                    req.RegularPrice,
                    req.PromotionalPrice,
                    req.Attributes,
                    req.Active,
                    req.SalesRule
                ), ct);
                return Results.NoContent();
            })
        .RequireAuthorization(AuthPolicies.Backoffice);

        // -------------------------------------------------------
        // DELETE SKU (VARIANT)
        // -------------------------------------------------------

        cat.MapDelete("/products/{productId:guid}/variants/{skuId:guid}",
            async (ISender sender, Guid productId, Guid skuId, CancellationToken ct) =>
            {
                await sender.Send(new DeleteSkuCommand(productId, skuId), ct);
                return Results.NoContent();
            })
        .RequireAuthorization(AuthPolicies.Backoffice);

        // -------------------------------------------------------
        // GET PRODUCT BY SLUG (vitrine)
        // -------------------------------------------------------

        cat.MapGet("/products/by-slug/{slug}",
            async (ISender sender, string slug, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetProductBySlugQuery(slug), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        // -------------------------------------------------------
        // GET PRODUCT BY ID
        // -------------------------------------------------------

        cat.MapGet("/products/{id:guid}",
            async (ISender sender, Guid id, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetProductByIdQuery(id), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        // -------------------------------------------------------
        // GET PAGED PRODUCTS
        // -------------------------------------------------------

        cat.MapGet("/products",
            async (ISender sender, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        {
            var dto = await sender.Send(new GetProductsQuery(page, pageSize), ct);
            return Results.Ok(dto);
        });

        // -------------------------------------------------------
        // PRODUCT IMAGES (multipart upload + manage)
        // -------------------------------------------------------

        cat.MapPost("/products/{id:guid}/images",
                async (ISender sender, Guid id, IFormFile file, CancellationToken ct) =>
                {
                    if (file is null || file.Length == 0)
                    {
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                ["file"] = ["Nenhum arquivo enviado."]
                            },
                            title: "Validation failed",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    await using var stream = file.OpenReadStream();
                    var dto = await sender.Send(new UploadProductImageCommand(
                        id,
                        stream,
                        file.FileName,
                        file.ContentType ?? "application/octet-stream",
                        file.Length), ct);
                    return Results.Created($"/api/catalog/products/{id}/images/{dto.Id}", dto);
                })
            .RequireAuthorization(AuthPolicies.Backoffice)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data");

        cat.MapDelete("/products/{productId:guid}/images/{imageId:guid}",
                async (ISender sender, Guid productId, Guid imageId, CancellationToken ct) =>
                {
                    await sender.Send(new DeleteProductImageCommand(productId, imageId), ct);
                    return Results.NoContent();
                })
            .RequireAuthorization(AuthPolicies.Backoffice);

        cat.MapPost("/products/{productId:guid}/images/{imageId:guid}/primary",
                async (ISender sender, Guid productId, Guid imageId, CancellationToken ct) =>
                {
                    await sender.Send(new SetPrimaryProductImageCommand(productId, imageId), ct);
                    return Results.NoContent();
                })
            .RequireAuthorization(AuthPolicies.Backoffice);

        return group;
    }
}

// Requests

public sealed record CreateVariantProductRequest(
    string Name,
    string Slug,
    Guid? CategoryId);

public sealed record AddVariantRequest(
    string? Code,
    decimal RegularPrice,
    decimal? PromotionalPrice,
    IReadOnlyList<SkuAttributeCreateDto>? Attributes,
    bool Active = true,
    SkuSalesRuleWriteDto? SalesRule = null);

public sealed record UpdateProductRequest(
    string Name,
    string? Slug,
    Guid? CategoryId,
    bool IsActive);

public sealed record UpdateVariantRequest(
    string? Code,
    decimal RegularPrice,
    decimal? PromotionalPrice,
    IReadOnlyList<SkuAttributeCreateDto>? Attributes,
    bool Active = true,
    SkuSalesRuleWriteDto? SalesRule = null);