namespace Vls.Shopflow.IdentityAccess.Domain.Entities;

public sealed class CustomerAddress
{
    public Guid Id { get; private set; }
    public Guid CustomerUserId { get; private set; }
    public string? Label { get; private set; }
    public string RecipientName { get; private set; } = default!;
    public string PostalCode { get; private set; } = default!;
    public string Street { get; private set; } = default!;
    public string Number { get; private set; } = default!;
    public string? Complement { get; private set; }
    public string Neighborhood { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string State { get; private set; } = default!;
    public string Country { get; private set; } = default!;
    public bool IsDefault { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private CustomerAddress() { }

    public static CustomerAddress Create(
        Guid customerUserId,
        string? label,
        string recipientName,
        string postalCodeDigits,
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        bool isDefault,
        string country = "BR")
    {
        if (customerUserId == Guid.Empty)
            throw new ArgumentException("CustomerUserId is required.", nameof(customerUserId));

        var now = DateTimeOffset.UtcNow;
        return new CustomerAddress
        {
            Id = Guid.NewGuid(),
            CustomerUserId = customerUserId,
            Label = NormalizeOptional(label, 40),
            RecipientName = recipientName.Trim(),
            PostalCode = postalCodeDigits,
            Street = street.Trim(),
            Number = number.Trim(),
            Complement = NormalizeOptional(complement, 120),
            Neighborhood = neighborhood.Trim(),
            City = city.Trim(),
            State = state.Trim().ToUpperInvariant(),
            Country = string.IsNullOrWhiteSpace(country) ? "BR" : country.Trim().ToUpperInvariant(),
            IsDefault = isDefault,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string? label,
        string recipientName,
        string postalCodeDigits,
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state)
    {
        Label = NormalizeOptional(label, 40);
        RecipientName = recipientName.Trim();
        PostalCode = postalCodeDigits;
        Street = street.Trim();
        Number = number.Trim();
        Complement = NormalizeOptional(complement, 120);
        Neighborhood = neighborhood.Trim();
        City = city.Trim();
        State = state.Trim().ToUpperInvariant();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDefault()
    {
        if (IsDefault)
            return;
        IsDefault = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ClearDefault()
    {
        if (!IsDefault)
            return;
        IsDefault = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
