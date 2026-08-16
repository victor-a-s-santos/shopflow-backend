using Microsoft.Extensions.Options;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Options;

namespace Vls.Shopflow.Worker;

public sealed class OrderEmailIntentDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderEmailIntentDispatcherOptions> options,
    ILogger<OrderEmailIntentDispatcherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Order email intent dispatcher is disabled by configuration");
            return;
        }

        var intervalSeconds = settings.IntervalSeconds <= 0 ? 15 : settings.IntervalSeconds;
        logger.LogInformation(
            "Order email intent dispatcher started. Interval={IntervalSeconds}s BatchSize={BatchSize}",
            intervalSeconds,
            settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOrderEmailIntentDispatcher>();
                await dispatcher.ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Order email intent dispatcher batch failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Order email intent dispatcher stopped");
    }
}
