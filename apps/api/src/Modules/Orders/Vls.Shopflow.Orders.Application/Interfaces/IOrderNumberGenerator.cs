namespace Vls.Shopflow.Orders.Application.Interfaces;

public interface IOrderNumberGenerator
{
    Task<long> NextAsync(CancellationToken cancellationToken = default);
}
