using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

/// <summary>
/// Fase 1 hook for CustomerRegisteredPendingApproval. Brevo templates are Fase 3.
/// </summary>
public sealed class LoggingCustomerPendingApprovalNotifier(ILogger<LoggingCustomerPendingApprovalNotifier> logger)
    : ICustomerPendingApprovalNotifier
{
    public Task NotifyRegisteredPendingAsync(
        CustomerRegisteredPendingApproval notification,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "CustomerRegisteredPendingApproval CustomerUserId={CustomerUserId} Email={Email} RequestedAt={RequestedAt}. Brevo approval e-mail reserved for Fase 3.",
            notification.CustomerUserId,
            notification.Email,
            notification.RequestedAt);
        return Task.CompletedTask;
    }
}
