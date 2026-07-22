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

/// <summary>
/// Purchase quantity violates the SKU sales rule (min/step/package configuration).
/// </summary>
public sealed class CheckoutSalesRuleViolationException : Exception
{
    public const string MinQuantity = "SALES_MIN_QUANTITY";
    public const string QuantityStep = "SALES_QUANTITY_STEP";
    public const string InvalidConfiguration = "SALES_RULE_INVALID_CONFIGURATION";
    public const string PackageInvalid = "SALES_PACKAGE_INVALID";

    public string Code { get; }
    public Guid SkuId { get; }
    public string Field { get; }

    public CheckoutSalesRuleViolationException(string code, Guid skuId, string message, string field = "quantity")
        : base(message)
    {
        Code = code;
        SkuId = skuId;
        Field = field;
    }
}
