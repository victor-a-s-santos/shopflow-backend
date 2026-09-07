using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

public interface ICustomerAddressService
{
    Task<IReadOnlyList<CustomerAddressDto>> ListAsync(
        Guid customerUserId,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressDto?> GetDefaultAsync(
        Guid customerUserId,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressDto?> GetOwnedAsync(
        Guid customerUserId,
        Guid addressId,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressResult> CreateAsync(
        Guid customerUserId,
        CustomerAddressWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressResult> UpdateAsync(
        Guid customerUserId,
        Guid addressId,
        CustomerAddressWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressResult> DeleteAsync(
        Guid customerUserId,
        Guid addressId,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressResult> SetDefaultAsync(
        Guid customerUserId,
        Guid addressId,
        CancellationToken cancellationToken = default);
}
