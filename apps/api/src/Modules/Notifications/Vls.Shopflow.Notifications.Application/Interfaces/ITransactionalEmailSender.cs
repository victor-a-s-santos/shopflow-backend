using Vls.Shopflow.Notifications.Application.Models;

namespace Vls.Shopflow.Notifications.Application.Interfaces;

public interface ITransactionalEmailSender
{
    Task<TransactionalEmailSendResult> SendAsync(
        TransactionalEmailMessage message,
        CancellationToken cancellationToken = default);
}
