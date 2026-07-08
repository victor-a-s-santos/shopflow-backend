using Microsoft.Extensions.Options;
using Vls.Shopflow.Expiration.Application.Interfaces;
using Vls.Shopflow.Expiration.Application.Options;

namespace Vls.Shopflow.Worker;

public sealed class PendingCheckoutExpirationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ExpirationWorkerOptions> options,
    ILogger<PendingCheckoutExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Expiration worker is disabled by configuration");
            return;
        }

        logger.LogInformation(
            "Expiration worker started. Interval={IntervalSeconds}s BatchSize={BatchSize}",
            settings.IntervalSeconds,
            settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IExpirationProcessor>();
                await processor.ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Expiration worker batch failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Expiration worker stopped");
    }
}
