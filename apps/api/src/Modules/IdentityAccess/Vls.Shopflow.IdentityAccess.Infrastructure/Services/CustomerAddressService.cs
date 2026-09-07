using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Services;
using Vls.Shopflow.IdentityAccess.Domain.Entities;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

public sealed class CustomerAddressService(IdentityAccessDbContext db) : ICustomerAddressService
{
    public async Task<IReadOnlyList<CustomerAddressDto>> ListAsync(
        Guid customerUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.CustomerUserId == customerUserId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<CustomerAddressDto?> GetDefaultAsync(
        Guid customerUserId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.CustomerUserId == customerUserId && a.IsDefault)
            .OrderByDescending(a => a.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : ToDto(row);
    }

    public async Task<CustomerAddressDto?> GetOwnedAsync(
        Guid customerUserId,
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == addressId && a.CustomerUserId == customerUserId,
                cancellationToken);

        return row is null ? null : ToDto(row);
    }

    public async Task<CustomerAddressResult> CreateAsync(
        Guid customerUserId,
        CustomerAddressWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = Validate(request);
        if (validated.Errors is not null)
            return validated.ToResult();

        var hasAny = await db.CustomerAddresses
            .AnyAsync(a => a.CustomerUserId == customerUserId, cancellationToken);
        var isDefault = !hasAny || request.SetAsDefault;

        if (isDefault)
            await ClearDefaultsAsync(customerUserId, exceptId: null, cancellationToken);

        var entity = CustomerAddress.Create(
            customerUserId,
            request.Label,
            validated.RecipientName,
            validated.PostalCodeDigits,
            validated.Street,
            validated.Number,
            request.Complement,
            validated.Neighborhood,
            validated.City,
            validated.State,
            isDefault);

        db.CustomerAddresses.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CustomerAddressResult.Ok(ToDto(entity));
    }

    public async Task<CustomerAddressResult> UpdateAsync(
        Guid customerUserId,
        Guid addressId,
        CustomerAddressWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = Validate(request);
        if (validated.Errors is not null)
            return validated.ToResult();

        var entity = await FindOwnedAsync(customerUserId, addressId, cancellationToken);
        if (entity is null)
            return NotFound();

        entity.Update(
            request.Label,
            validated.RecipientName,
            validated.PostalCodeDigits,
            validated.Street,
            validated.Number,
            request.Complement,
            validated.Neighborhood,
            validated.City,
            validated.State);

        if (request.SetAsDefault)
        {
            await ClearDefaultsAsync(customerUserId, entity.Id, cancellationToken);
            entity.MarkDefault();
        }

        await db.SaveChangesAsync(cancellationToken);
        return CustomerAddressResult.Ok(ToDto(entity));
    }

    public async Task<CustomerAddressResult> DeleteAsync(
        Guid customerUserId,
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnedAsync(customerUserId, addressId, cancellationToken);
        if (entity is null)
            return NotFound();

        var wasDefault = entity.IsDefault;
        db.CustomerAddresses.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        if (wasDefault)
        {
            var next = await db.CustomerAddresses
                .Where(a => a.CustomerUserId == customerUserId)
                .OrderByDescending(a => a.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            next?.MarkDefault();
            if (next is not null)
                await db.SaveChangesAsync(cancellationToken);
        }

        return CustomerAddressResult.Ok(ToDto(entity));
    }

    public async Task<CustomerAddressResult> SetDefaultAsync(
        Guid customerUserId,
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnedAsync(customerUserId, addressId, cancellationToken);
        if (entity is null)
            return NotFound();

        await ClearDefaultsAsync(customerUserId, entity.Id, cancellationToken);
        entity.MarkDefault();
        await db.SaveChangesAsync(cancellationToken);
        return CustomerAddressResult.Ok(ToDto(entity));
    }

    private async Task<CustomerAddress?> FindOwnedAsync(
        Guid customerUserId,
        Guid addressId,
        CancellationToken cancellationToken)
        => await db.CustomerAddresses
            .FirstOrDefaultAsync(
                a => a.Id == addressId && a.CustomerUserId == customerUserId,
                cancellationToken);

    private async Task ClearDefaultsAsync(
        Guid customerUserId,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var currentDefaults = await db.CustomerAddresses
            .Where(a => a.CustomerUserId == customerUserId && a.IsDefault)
            .Where(a => exceptId == null || a.Id != exceptId)
            .ToListAsync(cancellationToken);

        foreach (var item in currentDefaults)
            item.ClearDefault();
    }

    private static ValidatedWrite Validate(CustomerAddressWriteRequest request)
    {
        var errors = CustomerAddressRules.Validate(
            request.Label,
            request.RecipientName,
            request.PostalCode,
            request.Street,
            request.Number,
            request.Complement,
            request.Neighborhood,
            request.City,
            request.State);

        if (errors.Count > 0)
            return new ValidatedWrite(errors);

        return new ValidatedWrite(
            Errors: null,
            RecipientName: request.RecipientName!.Trim(),
            PostalCodeDigits: CustomerAddressRules.TryNormalizePostalCode(request.PostalCode)!,
            Street: request.Street.Trim(),
            Number: request.Number.Trim(),
            Neighborhood: request.Neighborhood.Trim(),
            City: request.City.Trim(),
            State: request.State.Trim().ToUpperInvariant());
    }

    private static CustomerAddressResult NotFound()
        => CustomerAddressResult.Fail("ADDRESS_NOT_FOUND", "Endereço não encontrado.");

    private static CustomerAddressDto ToDto(CustomerAddress entity)
        => new(
            entity.Id,
            entity.Label,
            entity.RecipientName,
            CustomerAddressRules.FormatPostalCode(entity.PostalCode),
            entity.Street,
            entity.Number,
            entity.Complement,
            entity.Neighborhood,
            entity.City,
            entity.State,
            entity.Country,
            entity.IsDefault,
            entity.CreatedAt,
            entity.UpdatedAt);

    private sealed record ValidatedWrite(
        IReadOnlyDictionary<string, string[]>? Errors,
        string RecipientName = "",
        string PostalCodeDigits = "",
        string Street = "",
        string Number = "",
        string Neighborhood = "",
        string City = "",
        string State = "")
    {
        public CustomerAddressResult ToResult()
            => CustomerAddressResult.Fail(
                "ADDRESS_VALIDATION_FAILED",
                "Verifique os campos do endereço.",
                Errors);
    }
}
