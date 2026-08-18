using Vls.Shopflow.Shipping.Application.DataTransferObjects;

namespace Vls.Shopflow.Shipping.Application.Interfaces;

public interface IPostalCodeLookupService
{
    /// <summary>
    /// Looks up a Brazilian CEP. <paramref name="cep"/> must already be 8 digits
    /// (caller validates). Returns <see cref="BrazilPostalCodeLookupDto.Found"/> = false
    /// when the provider reports unknown CEP. Throws
    /// <see cref="Exceptions.PostalCodeLookupUnavailableException"/> on provider/network failure.
    /// </summary>
    Task<BrazilPostalCodeLookupDto> LookupBrazilPostalCodeAsync(
        string cep,
        CancellationToken cancellationToken);
}
