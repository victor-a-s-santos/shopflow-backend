using Vls.Shopflow.CartCheckout.Application.Commands;

namespace Vls.Shopflow.CartCheckout.Application.Services;

internal static class CheckoutItemConsolidator
{
    public static IReadOnlyList<CheckoutItemInput> Consolidate(IReadOnlyList<CheckoutItemInput> items)
        => items
            .GroupBy(i => i.SkuId)
            .Select(g => new CheckoutItemInput(g.Key, g.Sum(x => x.Quantity)))
            .ToList();
}
