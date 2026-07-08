namespace Vls.Shopflow.Expiration.Application.Interfaces;

public interface IExpirationProcessor
{
    Task<ExpirationBatchResult> ProcessAsync(CancellationToken cancellationToken);
}
