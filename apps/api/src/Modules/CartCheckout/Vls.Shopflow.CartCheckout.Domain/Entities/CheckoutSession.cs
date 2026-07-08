using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.CartCheckout.Domain.Exceptions;

namespace Vls.Shopflow.CartCheckout.Domain.Entities;

public sealed class CheckoutSession : Entity<Guid>
{
    private readonly List<CheckoutSessionItem> _items = new();

    public CheckoutSessionStatus Status { get; private set; }
    public string CustomerName { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;
    public string CustomerPhone { get; private set; } = default!;
    public string AddressZipCode { get; private set; } = default!;
    public string AddressStreet { get; private set; } = default!;
    public string AddressNumber { get; private set; } = default!;
    public string? AddressComplement { get; private set; }
    public string AddressNeighborhood { get; private set; } = default!;
    public string AddressCity { get; private set; } = default!;
    public string AddressState { get; private set; } = default!;
    public decimal Subtotal { get; private set; }
    public decimal? ShippingAmount { get; private set; }
    public decimal Total { get; private set; }
    public DateTimeOffset ReservationExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }

    public IReadOnlyCollection<CheckoutSessionItem> Items => _items.AsReadOnly();

    private CheckoutSession() { }

    public static CheckoutSession CreatePending(
        string customerName,
        string customerEmail,
        string customerPhone,
        string addressZipCode,
        string addressStreet,
        string addressNumber,
        string? addressComplement,
        string addressNeighborhood,
        string addressCity,
        string addressState,
        DateTimeOffset reservationExpiresAt,
        IReadOnlyList<CheckoutSessionItem> items)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Checkout session must contain at least one item.");

        var now = DateTimeOffset.UtcNow;
        var subtotal = items.Sum(i => i.Subtotal);

        var session = new CheckoutSession
        {
            Id = Guid.NewGuid(),
            Status = CheckoutSessionStatus.Pending,
            CustomerName = customerName.Trim(),
            CustomerEmail = customerEmail.Trim(),
            CustomerPhone = customerPhone.Trim(),
            AddressZipCode = addressZipCode.Trim(),
            AddressStreet = addressStreet.Trim(),
            AddressNumber = addressNumber.Trim(),
            AddressComplement = string.IsNullOrWhiteSpace(addressComplement) ? null : addressComplement.Trim(),
            AddressNeighborhood = addressNeighborhood.Trim(),
            AddressCity = addressCity.Trim(),
            AddressState = addressState.Trim(),
            Subtotal = subtotal,
            ShippingAmount = null,
            Total = subtotal,
            ReservationExpiresAt = reservationExpiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var item in items)
        {
            item.AttachToSession(session.Id);
            session._items.Add(item);
        }

        return session;
    }

    public void Cancel()
    {
        if (Status == CheckoutSessionStatus.Canceled)
            return;

        if (Status != CheckoutSessionStatus.Pending)
            throw new InvalidCheckoutSessionStatusException(
                Id,
                $"Checkout session {Id} cannot be canceled because its status is {Status}.");

        Status = CheckoutSessionStatus.Canceled;
        CanceledAt = DateTimeOffset.UtcNow;
        UpdatedAt = CanceledAt.Value;
    }

    public void Expire()
    {
        if (Status == CheckoutSessionStatus.Expired)
            return;

        if (Status == CheckoutSessionStatus.Canceled)
            return;

        if (Status != CheckoutSessionStatus.Pending)
            throw new InvalidCheckoutSessionStatusException(
                Id,
                $"Checkout session {Id} cannot be expired because its status is {Status}.");

        var now = DateTimeOffset.UtcNow;
        Status = CheckoutSessionStatus.Expired;
        UpdatedAt = now;
    }
}
