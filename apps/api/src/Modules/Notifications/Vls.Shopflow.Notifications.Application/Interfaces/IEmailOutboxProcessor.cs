namespace Vls.Shopflow.Notifications.Application.Interfaces;

public interface IEmailOutboxProcessor
{
    Task ProcessAsync(CancellationToken cancellationToken = default);
}
