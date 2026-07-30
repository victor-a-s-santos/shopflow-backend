namespace Vls.Shopflow.Shipping.Application.DataTransferObjects;

/// <summary>
/// Normalized Brazil postal-code lookup result. Never exposes raw provider payloads.
/// </summary>
public sealed record BrazilPostalCodeLookupDto(
    string PostalCode,
    bool Found,
    string? Street = null,
    string? Neighborhood = null,
    string? City = null,
    string? State = null,
    string Country = "BR",
    string? Source = null);
