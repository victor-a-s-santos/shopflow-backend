using Vls.Shopflow.Orders.Domain.Constants;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;
using Vls.Shopflow.Orders.Domain.Services;

namespace Vls.Shopflow.Orders.Application.Services;

public sealed record DeliveryBatchCustomerIdentity(
    Guid? CustomerUserId,
    string? Name,
    string? Email,
    string? Phone,
    string? EmailNormalized,
    string? PhoneNormalized);

public sealed record DeliveryBatchAddressInfo(
    Guid OrderId,
    string OrderNumber,
    string AddressSummary,
    string Fingerprint);

/// <summary>
/// Validates eligibility and same-customer rules for DeliveryBatch operations.
/// </summary>
public static class DeliveryBatchGroupingRules
{
    public static void EnsureEligibleForBatch(Order order, bool alreadyInBatch)
    {
        if (alreadyInBatch)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderAlreadyInBatch,
                "Um ou mais pedidos já pertencem a uma entrega agrupada.");
        }

        if (order.Status == OrderStatus.PendingPayment)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderNotPaid,
                "Todos os pedidos precisam estar pagos e aguardando envio.");
        }

        if (order.Status is OrderStatus.Canceled or OrderStatus.Expired)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderNotEligible,
                "Todos os pedidos precisam estar pagos e aguardando envio.");
        }

        if (order.Status != OrderStatus.Paid)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderNotPaid,
                "Todos os pedidos precisam estar pagos e aguardando envio.");
        }

        if (order.FulfillmentStatus == FulfillmentStatus.Shipped)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderAlreadyShipped,
                "Todos os pedidos precisam estar pagos e aguardando envio.");
        }

        if (order.FulfillmentStatus == FulfillmentStatus.Delivered)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderAlreadyDelivered,
                "Todos os pedidos precisam estar pagos e aguardando envio.");
        }

        if (order.FulfillmentStatus != FulfillmentStatus.AwaitingShipment)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.OrderNotEligible,
                "Todos os pedidos precisam estar pagos e aguardando envio.");
        }
    }

    public static bool IsEligibleCandidate(Order order, bool alreadyInBatch)
        => !alreadyInBatch
           && order.Status == OrderStatus.Paid
           && order.FulfillmentStatus == FulfillmentStatus.AwaitingShipment;

    public static DeliveryBatchCustomerIdentity ResolveIdentity(IReadOnlyList<Order> orders)
    {
        if (orders.Count == 0)
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.CustomerIdentityRequired,
                "Não foi possível identificar o cliente dos pedidos selecionados.");
        }

        var withUserId = orders.Where(o => o.CustomerUserId is not null).ToList();
        if (withUserId.Count > 0)
        {
            if (withUserId.Count != orders.Count)
            {
                throw new DeliveryBatchException(
                    DeliveryBatchErrorCodes.CustomerMismatch,
                    "Todos os pedidos selecionados precisam pertencer ao mesmo cliente.");
            }

            var userId = withUserId[0].CustomerUserId!.Value;
            if (withUserId.Any(o => o.CustomerUserId != userId))
            {
                throw new DeliveryBatchException(
                    DeliveryBatchErrorCodes.CustomerMismatch,
                    "Todos os pedidos selecionados precisam pertencer ao mesmo cliente.");
            }

            var sample = withUserId[0];
            return new DeliveryBatchCustomerIdentity(
                userId,
                sample.CustomerFullName,
                sample.CustomerEmail,
                sample.CustomerPhone,
                CustomerContactNormalizer.NormalizeEmail(sample.CustomerEmail),
                CustomerContactNormalizer.NormalizePhone(sample.CustomerPhone));
        }

        // Guest: email + phone only (never name alone).
        var identities = orders
            .Select(o => (
                Email: CustomerContactNormalizer.NormalizeEmail(o.CustomerEmail),
                Phone: CustomerContactNormalizer.NormalizePhone(o.CustomerPhone),
                Order: o))
            .ToList();

        if (identities.Any(i => i.Email is null || i.Phone is null))
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.CustomerIdentityRequired,
                "Não foi possível identificar o cliente dos pedidos selecionados.");
        }

        var email = identities[0].Email!;
        var phone = identities[0].Phone!;
        if (identities.Any(i => i.Email != email || i.Phone != phone))
        {
            throw new DeliveryBatchException(
                DeliveryBatchErrorCodes.CustomerMismatch,
                "Todos os pedidos selecionados precisam pertencer ao mesmo cliente.");
        }

        var guest = identities[0].Order;
        return new DeliveryBatchCustomerIdentity(
            null,
            guest.CustomerFullName,
            guest.CustomerEmail,
            guest.CustomerPhone,
            email,
            phone);
    }

    public static IReadOnlyList<DeliveryBatchAddressInfo> BuildAddressInfos(IReadOnlyList<Order> orders)
        => orders.Select(o => new DeliveryBatchAddressInfo(
            o.Id,
            o.FormatOrderNumber(),
            CustomerContactNormalizer.AddressSummary(o.ShippingCity, o.ShippingState, o.ShippingZipCode),
            CustomerContactNormalizer.AddressFingerprint(
                o.ShippingZipCode,
                o.ShippingStreet,
                o.ShippingNumber,
                o.ShippingComplement,
                o.ShippingNeighborhood,
                o.ShippingCity,
                o.ShippingState))).ToList();

    public static bool HasDifferentAddresses(IReadOnlyList<DeliveryBatchAddressInfo> addresses)
        => addresses.Select(a => a.Fingerprint).Distinct(StringComparer.Ordinal).Count() > 1;
}
