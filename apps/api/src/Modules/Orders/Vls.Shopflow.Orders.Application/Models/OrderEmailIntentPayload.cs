using System.Text.Json;
using System.Text.Json.Serialization;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Models;

public sealed record OrderEmailIntentPayload(
    long OrderNumber,
    string CustomerEmail,
    string CustomerName,
    decimal Total,
    Guid? CustomerUserId = null,
    string? GuestAccessToken = null,
    string? TrackingCode = null,
    string? FinalDeliveryMethod = null,
    string? PreferredDeliveryMethod = null,
    DateOnly? PreferredDeliveryDate = null);

public static class OrderEmailIntentPayloadJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(OrderEmailIntentPayload payload)
        => JsonSerializer.Serialize(payload, Options);

    public static OrderEmailIntentPayload Deserialize(string json)
        => JsonSerializer.Deserialize<OrderEmailIntentPayload>(json, Options)
           ?? throw new InvalidOperationException("Order email intent payload is empty.");

    public static OrderEmailIntentPayload FromOrder(Order order, string? guestAccessToken = null)
        => new(
            order.OrderNumber,
            order.CustomerEmail,
            order.CustomerFullName,
            order.Total,
            order.CustomerUserId,
            guestAccessToken,
            order.TrackingCode,
            order.FinalDeliveryMethod?.ToString(),
            order.PreferredDeliveryMethod?.ToString(),
            order.PreferredDeliveryDate);
}
