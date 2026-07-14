using MediatR;

namespace Vls.Shopflow.PaymentsPix.Application.Commands;

public sealed record ProcessMercadoPagoPixWebhookCommand(
    string? DataIdFromQuery,
    string? DataIdFromBody,
    string? XSignature,
    string? XRequestId,
    string? Action,
    string? Type,
    bool LiveMode,
    string? ProviderEventId,
    string? ApplicationId = null,
    string? UserId = null,
    string? DataStatus = null,
    string? DataStatusDetail = null)
    : IRequest<ProcessMercadoPagoPixWebhookResult>;

public sealed record ProcessMercadoPagoPixWebhookResult(
    int StatusCode,
    string Outcome,
    string Message,
    Guid? PixPaymentId = null,
    Guid? OrderId = null);
