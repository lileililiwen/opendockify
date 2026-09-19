using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDockify.Integrations.Configuration;
using Platform.Jobs;

namespace OpenDockify.Integrations.Services;

/// <summary>
/// Recurring job that performs one webhook-delivery pass: enqueue
/// pending outbox events, claim due deliveries, deliver each one with
/// the platform-managed retry policy, and apply the retention sweep
/// every hour. Replaces the previous <c>BackgroundService</c>-based
/// <c>WebhookDeliveryWorker</c>; the platform is now responsible for
/// scheduling the pass. The handler is a no-op when
/// <c>Integrations:Webhooks:Enabled</c> is false, so a fresh
/// deployment is outbound-silent by default.
/// </summary>
[RecurringJob("*/1 * * * *", Name = "openDockify.integrations.webhookDelivery", TimeZone = "UTC")]
public sealed partial class WebhookDeliveryJobHandler : IRecurringJobHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<IntegrationsOptions> _options;
    private readonly ILogger<WebhookDeliveryJobHandler> _logger;

    public WebhookDeliveryJobHandler(
        IServiceScopeFactory scopeFactory,
        IOptions<IntegrationsOptions> options,
        ILogger<WebhookDeliveryJobHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var webhooks = _options.Value.Webhooks;
        if (!webhooks.Enabled)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WebhookDeliveryService>();
        try
        {
            var created = await service.EnqueuePendingEventsAsync(cancellationToken);
            if (created > 0)
            {
                Log.Enqueued(_logger, created);
            }

            var claimed = await service.ClaimDueDeliveriesAsync(batchSize: 20, cancellationToken);
            foreach (var delivery in claimed)
            {
                await service.DeliverAsync(delivery, cancellationToken);
            }

            var removed = await service.ApplyRetentionAsync(DateTime.UtcNow, cancellationToken);
            if (removed > 0)
            {
                Log.RetentionRemoved(_logger, removed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // recurring job must not fail the scheduler; the platform retries per its own policy.
        catch (Exception ex)
        {
            Log.PassFailed(_logger, ex);
            throw new InvalidOperationException("Webhook delivery pass failed; the next recurring tick will retry.", ex);
        }
#pragma warning restore CA1031
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Enqueued {Count} webhook deliveries from the outbox.")]
        public static partial void Enqueued(ILogger logger, int count);

        [LoggerMessage(2, LogLevel.Information, "Webhook retention removed {Count} terminal deliveries.")]
        public static partial void RetentionRemoved(ILogger logger, int count);

        [LoggerMessage(3, LogLevel.Error, "Webhook delivery pass failed; the next recurring tick will retry.")]
        public static partial void PassFailed(ILogger logger, Exception exception);
    }
}
