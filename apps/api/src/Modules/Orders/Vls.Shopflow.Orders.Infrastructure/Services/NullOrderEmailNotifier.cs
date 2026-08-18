using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

public sealed class NullOrderEmailNotifier : IOrderEmailNotifier
{
    public Task NotifyOrderCreatedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyPaymentConfirmedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyOrderShippedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyOrderDeliveredAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
