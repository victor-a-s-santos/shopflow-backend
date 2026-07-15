using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;

namespace Vls.Shopflow.Worker;

/// <summary>
/// Fallback poller for pending Mercado Pago Pix via GET /v1/orders. Does not replace webhooks.
/// </summary>
public sealed class MercadoPagoPixReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MercadoPagoReconciliationOptions> options,
    ILogger<MercadoPagoPixReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Mercado Pago Pix reconciliation worker is disabled by configuration");
            return;
        }

        var intervalSeconds = settings.IntervalSeconds <= 0 ? 60 : settings.IntervalSeconds;

        logger.LogInformation(
            "Mercado Pago Pix reconciliation worker started. Interval={IntervalSeconds}s BatchSize={BatchSize} MaxAgeMinutes={MaxAgeMinutes}",
            intervalSeconds,
            settings.BatchSize,
            settings.MaxAgeMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IMercadoPagoPixReconciliationProcessor>();
                await processor.ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Mercado Pago Pix reconciliation worker batch failed");
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

        logger.LogInformation("Mercado Pago Pix reconciliation worker stopped");
    }
}
