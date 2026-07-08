namespace Vls.Shopflow.CartCheckout.Domain.Exceptions;

public sealed class CheckoutSessionNotFoundException : Exception
{
    public Guid CheckoutSessionId { get; }

    public CheckoutSessionNotFoundException(Guid checkoutSessionId)
        : base($"Checkout session {checkoutSessionId} was not found.")
        => CheckoutSessionId = checkoutSessionId;
}

public sealed class InvalidCheckoutSessionStatusException : Exception
{
    public Guid CheckoutSessionId { get; }

    public InvalidCheckoutSessionStatusException(Guid checkoutSessionId, string message)
        : base(message)
        => CheckoutSessionId = checkoutSessionId;
}

public sealed class CatalogSkuNotFoundException : Exception
{
    public Guid SkuId { get; }

    public CatalogSkuNotFoundException(Guid skuId)
        : base($"SKU {skuId} was not found in catalog.")
        => SkuId = skuId;
}

public sealed class InactiveSkuException : Exception
{
    public Guid SkuId { get; }

    public InactiveSkuException(Guid skuId)
        : base($"SKU {skuId} is inactive and cannot be purchased.")
        => SkuId = skuId;
}
