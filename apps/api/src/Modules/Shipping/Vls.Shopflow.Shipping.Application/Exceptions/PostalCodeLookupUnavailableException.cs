namespace Vls.Shopflow.Shipping.Application.Exceptions;

/// <summary>
/// Provider disabled, timeout, or HTTP/network failure — not the same as CEP not found.
/// </summary>
public sealed class PostalCodeLookupUnavailableException : Exception
{
    public const string ErrorCode = "POSTAL_CODE_LOOKUP_UNAVAILABLE";

    public PostalCodeLookupUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
