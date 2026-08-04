namespace Vls.Shopflow.Notifications.Application.Models;

public sealed record TransactionalEmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string HtmlContent,
    string? TextContent = null);

public sealed record TransactionalEmailSendResult(
    bool Succeeded,
    string? ProviderMessageId,
    string? ErrorMessage,
    bool IsTransientFailure);
