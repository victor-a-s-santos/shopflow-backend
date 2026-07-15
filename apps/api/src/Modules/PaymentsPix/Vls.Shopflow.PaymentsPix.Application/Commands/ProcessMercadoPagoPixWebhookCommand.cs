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
    string? DataStatusDetail = null,
    // TEMPORARY DIAGNOSTIC ONLY — populated by HTTP layer for raw capture in Testing/HML.
    string? RawQueryString = null,
    string? QueryTypeExact = null,
    string? BodyRawJson = null,
    string? RequestPath = null,
    string? RequestMethod = null)
    : IRequest<ProcessMercadoPagoPixWebhookResult>;

public sealed record ProcessMercadoPagoPixWebhookResult(
    int StatusCode,
    string Outcome,
    string Message,
    Guid? PixPaymentId = null,
    Guid? OrderId = null);
