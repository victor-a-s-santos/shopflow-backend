using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Domain.Entities;

public sealed class Order : Entity<Guid>
{
    private readonly List<OrderItem> _items = new();

    public Guid CheckoutSessionId { get; private set; }
    public string CustomerFullName { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;
    public string CustomerPhone { get; private set; } = default!;
    public string ShippingZipCode { get; private set; } = default!;
    public string ShippingStreet { get; private set; } = default!;
    public string ShippingNumber { get; private set; } = default!;
    public string? ShippingComplement { get; private set; }
    public string ShippingNeighborhood { get; private set; } = default!;
    public string ShippingCity { get; private set; } = default!;
    public string ShippingState { get; private set; } = default!;
    public decimal Subtotal { get; private set; }
    public decimal? ShippingAmount { get; private set; }
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    /// <summary>
    /// Authenticated customer who owned the checkout when the order was created.
    /// Null for guest checkout — never resolve by email alone for “Meus pedidos”.
    /// </summary>
    public Guid? CustomerUserId { get; private set; }

    /// <summary>
    /// Friendly unique order number for UI and support (not the internal Guid).
    /// Assigned at creation via <see cref="AssignOrderNumber"/>.
    /// </summary>
    public long OrderNumber { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public static Order CreatePendingPayment(
        Guid checkoutSessionId,
        string customerFullName,
        string customerEmail,
        string customerPhone,
        string shippingZipCode,
        string shippingStreet,
        string shippingNumber,
        string? shippingComplement,
        string shippingNeighborhood,
        string shippingCity,
        string shippingState,
        decimal subtotal,
        decimal? shippingAmount,
        decimal total,
        IReadOnlyList<OrderItem> items,
        Guid? customerUserId = null)
    {
        if (checkoutSessionId == Guid.Empty)
            throw new ArgumentException("Checkout session id is required.", nameof(checkoutSessionId));

        if (string.IsNullOrWhiteSpace(customerEmail))
            throw new ArgumentException("Customer email is required.", nameof(customerEmail));

        if (items.Count == 0)
            throw new InvalidOperationException("Order must contain at least one item.");

        if (subtotal < 0)
            throw new ArgumentOutOfRangeException(nameof(subtotal), "Order subtotal cannot be negative.");

        if (total < 0)
            throw new ArgumentOutOfRangeException(nameof(total), "Order total cannot be negative.");

        if (customerUserId == Guid.Empty)
            throw new ArgumentException("Customer user id cannot be empty.", nameof(customerUserId));

        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CheckoutSessionId = checkoutSessionId,
            CustomerFullName = customerFullName.Trim(),
            CustomerEmail = customerEmail.Trim(),
            CustomerPhone = customerPhone.Trim(),
            ShippingZipCode = shippingZipCode.Trim(),
            ShippingStreet = shippingStreet.Trim(),
            ShippingNumber = shippingNumber.Trim(),
            ShippingComplement = string.IsNullOrWhiteSpace(shippingComplement) ? null : shippingComplement.Trim(),
            ShippingNeighborhood = shippingNeighborhood.Trim(),
            ShippingCity = shippingCity.Trim(),
            ShippingState = shippingState.Trim(),
            Subtotal = subtotal,
            ShippingAmount = shippingAmount,
            Total = total,
            Status = OrderStatus.PendingPayment,
            CustomerUserId = customerUserId,
            OrderNumber = 0,
            CreatedAt = now,
            UpdatedAt = null,
            PaidAt = null,
            CanceledAt = null
        };

        foreach (var item in items)
        {
            item.AttachToOrder(order.Id);
            order._items.Add(item);
        }

        return order;
    }

    /// <summary>
    /// Sets the friendly order number once, before persistence.
    /// </summary>
    public void AssignOrderNumber(long orderNumber)
    {
        if (OrderNumber != 0)
            throw new InvalidOperationException($"Order {Id} already has order number {OrderNumber}.");

        if (orderNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderNumber), "Order number must be positive.");

        OrderNumber = orderNumber;
    }

    public string FormatOrderNumber()
        => OrderNumber > 0 ? OrderNumber.ToString() : Id.ToString();

    public void MarkAsPaid(DateTimeOffset? paidAt = null)
    {
        if (Status == OrderStatus.Paid)
            return;

        if (Status != OrderStatus.PendingPayment)
            throw new InvalidOperationException(
                $"Order {Id} cannot be marked as Paid because its status is {Status}.");

        Status = OrderStatus.Paid;
        PaidAt = paidAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Expire()
    {
        if (Status == OrderStatus.Expired)
            return;

        if (Status == OrderStatus.Paid)
            return;

        if (Status == OrderStatus.Canceled)
            return;

        if (Status != OrderStatus.PendingPayment)
            throw new InvalidOperationException(
                $"Order {Id} cannot be expired because its status is {Status}.");

        Status = OrderStatus.Expired;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Links a guest order to an authenticated customer. Idempotent when already linked to the same user.
    /// </summary>
    public void LinkToCustomerUser(Guid customerUserId)
    {
        if (customerUserId == Guid.Empty)
            throw new ArgumentException("Customer user id cannot be empty.", nameof(customerUserId));

        if (CustomerUserId is null)
        {
            CustomerUserId = customerUserId;
            UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        if (CustomerUserId == customerUserId)
            return;

        throw new Exceptions.OrderAlreadyLinkedToAnotherCustomerException(Id);
    }
}
