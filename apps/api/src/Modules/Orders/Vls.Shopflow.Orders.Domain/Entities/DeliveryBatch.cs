using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Constants;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;
using Vls.Shopflow.Orders.Domain.Services;

namespace Vls.Shopflow.Orders.Domain.Entities;

public sealed class DeliveryBatchOrder : Entity<Guid>
{
    public Guid DeliveryBatchId { get; private set; }
    public Guid OrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private DeliveryBatchOrder() { }

    internal static DeliveryBatchOrder Create(Guid deliveryBatchId, Guid orderId, DateTimeOffset createdAt)
    {
        if (deliveryBatchId == Guid.Empty)
            throw new ArgumentException("Delivery batch id is required.", nameof(deliveryBatchId));
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));

        return new DeliveryBatchOrder
        {
            Id = Guid.NewGuid(),
            DeliveryBatchId = deliveryBatchId,
            OrderId = orderId,
            CreatedAt = createdAt
        };
    }
}

public sealed class DeliveryBatch : Entity<Guid>
{
    public const int TrackingCodeMaxLength = 120;
    public const int InternalNoteMaxLength = 2000;
    public const int MinOrders = 2;

    private readonly List<DeliveryBatchOrder> _orders = new();

    public long BatchNumber { get; private set; }
    public Guid? CustomerUserId { get; private set; }
    public string? CustomerName { get; private set; }
    public string? CustomerEmail { get; private set; }
    public string? CustomerEmailNormalized { get; private set; }
    public string? CustomerPhone { get; private set; }
    public string? CustomerPhoneNormalized { get; private set; }
    public DeliveryMethod? DeliveryMethod { get; private set; }
    public DeliveryBatchStatus Status { get; private set; }
    public string? TrackingCode { get; private set; }
    public string? InternalNote { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedByAdminId { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedByAdminId { get; private set; }
    public bool HasDifferentDeliveryAddresses { get; private set; }

    public IReadOnlyCollection<DeliveryBatchOrder> Orders => _orders.AsReadOnly();

    private DeliveryBatch() { }

    public static DeliveryBatch CreateAwaitingShipment(
        IReadOnlyList<Guid> orderIds,
        Guid? customerUserId,
        string? customerName,
        string? customerEmail,
        string? customerPhone,
        bool hasDifferentDeliveryAddresses,
        Guid? createdByAdminId,
        DeliveryMethod? deliveryMethod = null,
        string? trackingCode = null,
        string? internalNote = null)
    {
        if (orderIds is null || orderIds.Count == 0)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderIdsRequired,
                "Informe os pedidos para criar uma entrega agrupada.");
        }

        var distinct = orderIds.Distinct().ToList();
        if (distinct.Count < MinOrders)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.MinOrdersRequired,
                "Selecione pelo menos dois pedidos para criar uma entrega agrupada.");
        }

        if (distinct.Count != orderIds.Count)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderIdsRequired,
                "Há pedidos duplicados na seleção.");
        }

        var emailNorm = CustomerContactNormalizer.NormalizeEmail(customerEmail);
        var phoneNorm = CustomerContactNormalizer.NormalizePhone(customerPhone);

        if (customerUserId is null && (emailNorm is null || phoneNorm is null))
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.CustomerIdentityRequired,
                "Não foi possível identificar o cliente dos pedidos selecionados.");
        }

        var now = DateTimeOffset.UtcNow;
        var batch = new DeliveryBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = 0,
            CustomerUserId = customerUserId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail.Trim(),
            CustomerEmailNormalized = emailNorm,
            CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone.Trim(),
            CustomerPhoneNormalized = phoneNorm,
            DeliveryMethod = deliveryMethod,
            Status = DeliveryBatchStatus.AwaitingShipment,
            TrackingCode = NormalizeTracking(trackingCode),
            InternalNote = NormalizeNote(internalNote),
            HasDifferentDeliveryAddresses = hasDifferentDeliveryAddresses,
            CreatedAt = now,
            CreatedByAdminId = createdByAdminId is null || createdByAdminId == Guid.Empty
                ? null
                : createdByAdminId,
            UpdatedAt = null,
            UpdatedByAdminId = null
        };

        foreach (var orderId in distinct)
            batch._orders.Add(DeliveryBatchOrder.Create(batch.Id, orderId, now));

        return batch;
    }

    public void AssignBatchNumber(long batchNumber)
    {
        if (BatchNumber != 0)
            throw new InvalidOperationException($"Delivery batch {Id} already has batch number {BatchNumber}.");

        if (batchNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchNumber), "Batch number must be positive.");

        BatchNumber = batchNumber;
    }

    public string FormatBatchNumber()
        => BatchNumber > 0 ? BatchNumber.ToString() : Id.ToString();

    public void SetInternalNote(string? internalNote)
    {
        InternalNote = NormalizeNote(internalNote);
        Touch(DateTimeOffset.UtcNow, adminId: null);
    }

    /// <summary>
    /// Marks batch as shipped. Idempotent when already Shipped (updates method/tracking/note).
    /// Does not allow ship after Delivered.
    /// </summary>
    public void MarkAsShipped(
        Guid? adminId,
        DeliveryMethod? deliveryMethod = null,
        string? trackingCode = null,
        string? internalNote = null,
        DateTimeOffset? shippedAt = null)
    {
        if (Status == DeliveryBatchStatus.Delivered)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.AlreadyDelivered,
                "Esta entrega agrupada já foi entregue e não pode voltar para enviada.");
        }

        if (Status is not (DeliveryBatchStatus.AwaitingShipment or DeliveryBatchStatus.Shipped))
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.CannotBeShipped,
                "Esta entrega agrupada não pode ser marcada como enviada.");
        }

        var now = DateTimeOffset.UtcNow;
        Status = DeliveryBatchStatus.Shipped;
        ShippedAt = shippedAt ?? ShippedAt ?? now;
        if (deliveryMethod is not null)
            DeliveryMethod = deliveryMethod;
        if (trackingCode is not null)
            TrackingCode = NormalizeTracking(trackingCode);
        if (internalNote is not null)
            InternalNote = NormalizeNote(internalNote);

        Touch(now, adminId);
    }

    public void MarkAsDelivered(
        Guid? adminId,
        string? internalNote = null,
        DateTimeOffset? deliveredAt = null)
    {
        if (Status == DeliveryBatchStatus.Delivered)
        {
            if (internalNote is not null)
                InternalNote = NormalizeNote(internalNote);
            Touch(DateTimeOffset.UtcNow, adminId);
            return;
        }

        if (Status != DeliveryBatchStatus.Shipped)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.MustBeShippedBeforeDelivered,
                "A entrega agrupada precisa estar marcada como enviada antes de ser entregue.");
        }

        var now = DateTimeOffset.UtcNow;
        Status = DeliveryBatchStatus.Delivered;
        DeliveredAt = deliveredAt ?? now;
        if (internalNote is not null)
            InternalNote = NormalizeNote(internalNote);

        Touch(now, adminId);
    }

    private void Touch(DateTimeOffset at, Guid? adminId)
    {
        UpdatedAt = at;
        if (adminId is not null && adminId != Guid.Empty)
            UpdatedByAdminId = adminId;
    }

    private static string? NormalizeTracking(string? trackingCode)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
            return null;

        var trimmed = trackingCode.Trim();
        if (trimmed.Length > TrackingCodeMaxLength)
        {
            throw new DeliveryBatchException(
                OrderFulfillmentErrorCodes.TrackingCodeTooLong,
                "O código/rastreamento deve ter no máximo 120 caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return null;

        var trimmed = note.Trim();
        if (trimmed.Length > InternalNoteMaxLength)
        {
            throw new DeliveryBatchException(
                OrderFulfillmentErrorCodes.InternalNoteTooLong,
                "A observação interna deve ter no máximo 2000 caracteres.");
        }

        return trimmed;
    }
}
