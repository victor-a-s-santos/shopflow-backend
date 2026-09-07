namespace Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

public sealed record CustomerAddressDto(
    Guid Id,
    string? Label,
    string RecipientName,
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string Country,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerAddressWriteRequest(
    string? Label,
    string? RecipientName,
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    bool SetAsDefault = false);

public sealed record CustomerAddressResult(
    bool Succeeded,
    CustomerAddressDto? Address = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static CustomerAddressResult Ok(CustomerAddressDto address)
        => new(true, address);

    public static CustomerAddressResult Fail(
        string errorCode,
        string errorMessage,
        IReadOnlyDictionary<string, string[]>? errors = null)
        => new(false, null, errorCode, errorMessage, errors);
}
