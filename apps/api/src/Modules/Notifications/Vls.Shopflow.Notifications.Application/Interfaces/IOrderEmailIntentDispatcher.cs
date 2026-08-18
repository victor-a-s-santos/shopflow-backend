namespace Vls.Shopflow.Notifications.Application.Interfaces;

public interface IOrderEmailIntentDispatcher
{
    Task ProcessAsync(CancellationToken cancellationToken = default);
}
