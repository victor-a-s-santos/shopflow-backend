namespace Vls.Shopflow.PaymentsPix.Application.DataTransferObjects;

public sealed record PixPaymentDto(
    Guid PaymentId,
    Guid OrderId,
    string Status,
    string Provider,
    decimal Amount,
    string? QrCode,
    string? QrCodeImageUrl,
    string? CopyPasteCode,
    string? TicketUrl,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    string Message);

public sealed record CreatePixPaymentResult(PixPaymentDto Payment, bool WasCreated);
