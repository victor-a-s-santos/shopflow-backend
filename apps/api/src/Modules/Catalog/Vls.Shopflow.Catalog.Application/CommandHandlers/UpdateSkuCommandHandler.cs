using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Domain.Exceptions;

namespace Vls.Shopflow.Catalog.Application.CommandHandlers;

public sealed class UpdateSkuCommandHandler(
    IProductRepository productRepository,
    ICatalogUnitOfWork uow,
    IAttributeDefinitionLookup attributeLookup,
    ISkuLifecycleGuard lifecycleGuard)
    : IRequestHandler<UpdateSkuCommand>
{
    public async Task Handle(UpdateSkuCommand cmd, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(cmd.ProductId, cancellationToken);
        if (product is null)
            throw new KeyNotFoundException("Product not found.");

        var sku = product.GetSku(cmd.SkuId);
        if (sku is null)
            throw new KeyNotFoundException("SKU not found.");

        var newAttributes = await SkuAttributeFactory.CreateFromDtosAsync(
            cmd.Attributes,
            attributeLookup,
            "attributes",
            cancellationToken);

        // Empty code on update keeps the current code (avoids accidental regeneration).
        string nextCode;
        if (SkuCodeGenerator.IsEmpty(cmd.Code))
        {
            nextCode = sku.Code;
        }
        else
        {
            nextCode = SkuCodeGenerator.Normalize(cmd.Code!);
            if (string.IsNullOrEmpty(nextCode))
            {
                throw new ValidationException([
                    new ValidationFailure("code", "O código da SKU é inválido após normalização.")
                ]);
            }
        }

        var codeChanged = !string.Equals(sku.Code, nextCode, StringComparison.Ordinal);
        if (codeChanged)
        {
            var protection = await lifecycleGuard.GetProtectionAsync(sku.Id, cancellationToken);
            if (protection.BlocksCodeChange)
            {
                throw new CatalogConflictException(
                    "Não é possível alterar o código de uma SKU que possui estoque, movimentações ou histórico de pedidos. Inative a variação se necessário.",
                    CatalogErrorCodes.SkuCodeChangeProtected,
                    "code");
            }

            var duplicate = product.Skus.Any(s =>
                s.Id != sku.Id &&
                string.Equals(SkuCodeGenerator.Normalize(s.Code), nextCode, StringComparison.Ordinal));

            if (duplicate)
            {
                throw new CatalogConflictException(
                    $"O código “{nextCode}” já está sendo usado por outra variação deste produto.",
                    CatalogErrorCodes.SkuCodeDuplicate,
                    "code");
            }

            sku.ChangeCode(nextCode);
        }

        sku.ChangePrice(Price.From(cmd.RegularPrice, cmd.PromotionalPrice));

        // Omit salesRule on update → preserve existing (do not reset to Unit).
        // Explicit salesMode Unit is required to reset.
        if (cmd.SalesRule is not null)
            sku.ChangeSalesRule(SkuSalesRuleFactory.FromWriteDto(cmd.SalesRule));

        sku.ReplaceAttributes(newAttributes);

        if (cmd.Active)
            sku.Activate();
        else
            sku.Deactivate();

        await uow.SaveChangesAsync(cancellationToken);
    }
}
