using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDockify.Integrations.Configuration;

namespace OpenDockify.Integrations.Services;

/// <summary>
/// Background webhook delivery worker. Makes no outbound requests unless
/// <c>Integrations:Webhooks:Enabled</c> is true; otherwise it idles so a fresh
/// deployment is outbound-silent by default.
/// </summary>
public sealed partial class WebhookDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IntegrationsOptions> options,
    ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.WorkerStarted(logger, options.Value.Webhooks.Enabled, options.Value.Webhooks.GetPollInterval().TotalSeconds);

        var nextRetentionSweep = DateTime.UtcNow.AddHours(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.Value.Webhooks.GetPollInterval(), stoppingToken);
                if (!options.Value.Webhooks.Enabled)
                {
                    continue;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<WebhookDeliveryService>();

                var created = await service.EnqueuePendingEventsAsync(stoppingToken);
                if (created > 0)
                {
                    Log.Enqueued(logger, created);
                }

                var claimed = await service.ClaimDueDeliveriesAsync(batchSize: 20, stoppingToken);
                foreach (var delivery in claimed)
                {
                    await service.DeliverAsync(delivery, stoppingToken);
                }

                if (DateTime.UtcNow >= nextRetentionSweep)
                {
                    var removed = await service.ApplyRetentionAsync(DateTime.UtcNow, stoppingToken);
                    if (removed > 0)
                    {
                        Log.RetentionRemoved(logger, removed);
                    }

                    nextRetentionSweep = DateTime.UtcNow.AddHours(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.PassFailed(logger, ex);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information,
            "Webhook delivery worker started (enabled={Enabled}, pollInterval={IntervalSeconds}s).")]
        public static partial void WorkerStarted(ILogger logger, bool enabled, double intervalSeconds);

        [LoggerMessage(2, LogLevel.Information, "Enqueued {Count} webhook deliveries from the outbox.")]
        public static partial void Enqueued(ILogger logger, int count);

        [LoggerMessage(3, LogLevel.Information, "Webhook retention removed {Count} terminal deliveries.")]
        public static partial void RetentionRemoved(ILogger logger, int count);

        [LoggerMessage(4, LogLevel.Error, "Webhook delivery pass failed; retrying on the next tick.")]
        public static partial void PassFailed(ILogger logger, Exception exception);
    }
}
