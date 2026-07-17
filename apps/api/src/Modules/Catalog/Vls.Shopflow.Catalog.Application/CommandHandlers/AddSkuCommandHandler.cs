using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.Exceptions;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class AddSkuCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork catalogUnitOfWork,
    IAttributeDefinitionLookup attributeLookup)
    : IRequestHandler<AddSkuCommand, Guid>
{
    public async Task<Guid> Handle(AddSkuCommand cmd, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(cmd.ProductId, cancellationToken)
                      ?? throw new KeyNotFoundException("Product not found");

        var attributes = await SkuAttributeFactory.CreateFromDtosAsync(
            cmd.Attributes,
            attributeLookup,
            "attributes",
            cancellationToken);

        var existingCodes = product.Skus
            .Select(s => SkuCodeGenerator.Normalize(s.Code))
            .ToHashSet(StringComparer.Ordinal);

        string code;
        if (SkuCodeGenerator.IsEmpty(cmd.Code))
        {
            var labels = await SkuAttributeFactory.ResolveValueLabelsAsync(
                cmd.Attributes, attributeLookup, cancellationToken);
            code = SkuCodeGenerator.GenerateUnique(product.Name, labels, existingCodes);
        }
        else
        {
            code = SkuCodeGenerator.Normalize(cmd.Code!);
            if (string.IsNullOrEmpty(code))
            {
                throw new ValidationException([
                    new ValidationFailure("code", "O código da SKU é inválido após normalização.")
                ]);
            }

            if (existingCodes.Contains(code))
            {
                throw new CatalogConflictException(
                    $"O código “{code}” já está sendo usado por outra variação deste produto.",
                    CatalogErrorCodes.SkuCodeDuplicate,
                    "code");
            }
        }

        var price = Price.From(cmd.RegularPrice, cmd.PromotionalPrice);
        var sku = Sku.Create(product.Id, code, price, attributes, cmd.Active);
        product.AddSku(sku);

        await catalogUnitOfWork.SaveChangesAsync(cancellationToken);
        return sku.Id;
    }
}
