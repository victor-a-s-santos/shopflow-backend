using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Domain.Exceptions;

namespace Vls.Shopflow.CartCheckout.Application.Services;

public static class CheckoutSalesRuleValidator
{
    /// <summary>
    /// Validates purchase quantity against the SKU sales rule.
    /// Does not multiply by packageSize — quantity is always SKU units (packages or pieces).
    /// </summary>
    public static void EnsurePurchaseQuantityAllowed(Guid skuId, int quantity, SkuSalesRuleSnapshot rule)
    {
        if (quantity <= 0)
        {
            throw new CheckoutSalesRuleViolationException(
                CheckoutSalesRuleViolationException.MinQuantity,
                skuId,
                "A quantidade deve ser maior que zero.");
        }

        if (rule.IsPackageMode && (rule.PackageSize is null or <= 1))
        {
            throw new CheckoutSalesRuleViolationException(
                CheckoutSalesRuleViolationException.InvalidConfiguration,
                skuId,
                "Este pacote está configurado incorretamente. Entre em contato com o suporte.",
                "salesRule.packageSize");
        }

        if (rule.MinimumQuantity < 1 || rule.QuantityStep < 1)
        {
            throw new CheckoutSalesRuleViolationException(
                CheckoutSalesRuleViolationException.InvalidConfiguration,
                skuId,
                "Este pacote está configurado incorretamente. Entre em contato com o suporte.",
                "salesRule");
        }

        if (string.Equals(rule.SalesMode, "MultipleQuantity", StringComparison.OrdinalIgnoreCase)
            && rule.MinimumQuantity % rule.QuantityStep != 0)
        {
            throw new CheckoutSalesRuleViolationException(
                CheckoutSalesRuleViolationException.InvalidConfiguration,
                skuId,
                "Este pacote está configurado incorretamente. Entre em contato com o suporte.",
                "salesRule.minimumQuantity");
        }

        if (quantity < rule.MinimumQuantity)
        {
            throw new CheckoutSalesRuleViolationException(
                CheckoutSalesRuleViolationException.MinQuantity,
                skuId,
                $"Quantidade mínima deste produto é {rule.MinimumQuantity}.");
        }

        if ((quantity - rule.MinimumQuantity) % rule.QuantityStep != 0)
        {
            var message = rule.QuantityStep > 1
                ? $"Este produto é vendido em múltiplos de {rule.QuantityStep}."
                : $"Quantidade inválida para este produto.";

            throw new CheckoutSalesRuleViolationException(
                CheckoutSalesRuleViolationException.QuantityStep,
                skuId,
                message);
        }
    }
}
