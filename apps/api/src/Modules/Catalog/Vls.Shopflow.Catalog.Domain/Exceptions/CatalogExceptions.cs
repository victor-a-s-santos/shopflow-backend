namespace Vls.Shopflow.Catalog.Domain.Exceptions;

/// <summary>
/// Conflict (HTTP 409) for catalog uniqueness / lifecycle protection.
/// </summary>
public sealed class CatalogConflictException : Exception
{
    public string ErrorCode { get; }
    public string? Field { get; }

    public CatalogConflictException(string message, string errorCode, string? field = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Field = field;
    }
}

public static class CatalogErrorCodes
{
    public const string SkuCodeDuplicate = "SKU_CODE_DUPLICATE";
    public const string SkuDeleteProtected = "SKU_DELETE_PROTECTED";
    public const string SkuCodeChangeProtected = "SKU_CODE_CHANGE_PROTECTED";
    public const string ProductImageLimit = "PRODUCT_IMAGE_LIMIT";
    public const string ProductImageNotFound = "PRODUCT_IMAGE_NOT_FOUND";
}
