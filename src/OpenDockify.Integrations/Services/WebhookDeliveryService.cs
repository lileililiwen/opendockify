using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenDockify.Integrations.Configuration;
using OpenDockify.Integrations.Models;

namespace OpenDockify.Integrations.Services;

/// <summary>Redacted delivery view for owner observability (no secrets, no bodies).</summary>
public sealed record WebhookDeliveryView(
    Guid Id,
    Guid SubscriptionId,
    Guid EventId,
    string State,
    int AttemptCount,
    int? LastStatusCode,
    string? LastError,
    string? BlockedReason,
    DateTime CreatedAtUtc,
    DateTime? NextAttemptAtUtc,
    DateTime? DeliveredAtUtc,
    IReadOnlyList<DeliveryAttempt> Attempts);

/// <summary>One recorded delivery attempt (redacted).</summary>
public sealed record DeliveryAttempt(DateTime AtUtc, int? StatusCode, string? Error);

/// <summary>Plan-failure reasons that are transient and keep auto-retrying.</summary>
public static class TransientDeliveryBlockReasons
{
    public static readonly IReadOnlyCollection<string> All = ["dns-resolution-failed"];

    public static bool IsTransient(string? blockedReason)
    {
        return blockedReason is not null && All.Contains(blockedReason);
    }
}

public sealed record WebhookSubscriptionView(
    Guid Id,
    string Url,
    string EventTypes,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>
/// Webhook fan-out: outbox events become one delivery per matching active
/// subscription; a leased worker delivers with HMAC-signed envelopes, bounded
/// exponential retry, and SSRF-safe routing. Delivery is at-least-once:
/// receivers deduplicate by event id.
/// </summary>
public sealed partial class WebhookDeliveryService(
    DbContext db,
    WebhookRoutePlanner routePlanner,
    IWebhookSender sender,
    IOptions<IntegrationsOptions> options,
    ILogger<WebhookDeliveryService>? logger = null)
{
    private readonly ILogger<WebhookDeliveryService> _logger = logger ?? NullLogger<WebhookDeliveryService>.Instance;

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    public async Task<int> EnqueuePendingEventsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.Set<OutboxEvent>()
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return 0;
        }

        var created = 0;
        var now = DateTime.UtcNow;
        foreach (var @event in pending)
        {
            var subscriptions = await db.Set<WebhookSubscription>()
                .Where(x => x.OwnerId == @event.OwnerId && x.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var subscription in subscriptions.Where(x => SubscribesTo(x, @event.Type)))
            {
                db.Set<WebhookDelivery>().Add(new WebhookDelivery
                {
                    Id = Guid.NewGuid(),
                    SubscriptionId = subscription.Id,
                    OwnerId = @event.OwnerId,
                    EventId = @event.Id,
                    State = WebhookDeliveryState.Pending,
                    NextAttemptAtUtc = now,
                });
                created++;
            }

            @event.ProcessedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>
    /// Atomically claims due deliveries with a database lease. Deliveries whose
    /// lease expired (crashed worker) are reclaimed automatically.
    /// </summary>
    public async Task<List<WebhookDelivery>> ClaimDueDeliveriesAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var workerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var leaseExpires = now.AddMinutes(options.Value.Webhooks.GetLeaseMinutes());

        await db.Set<WebhookDelivery>()
            .Where(x => x.State == WebhookDeliveryState.Pending && x.NextAttemptAtUtc <= now
                        || x.State == WebhookDeliveryState.Delivering && x.LeaseExpiresAtUtc <= now)
            .OrderBy(x => x.NextAttemptAtUtc)
            .Take(batchSize)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, WebhookDeliveryState.Delivering)
                .SetProperty(x => x.LeaseOwner, workerId)
                .SetProperty(x => x.LeaseExpiresAtUtc, leaseExpires)
                .SetProperty(x => x.UpdatedAtUtc, now),
            cancellationToken);

        return await db.Set<WebhookDelivery>()
            .Where(x => x.LeaseOwner == workerId && x.LeaseExpiresAtUtc == leaseExpires)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Executes one delivery attempt: plan → sign → send → record.</summary>
    public async Task DeliverAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Set<WebhookSubscription>()
            .FindAsync([delivery.SubscriptionId], cancellationToken);
        var @event = await db.Set<OutboxEvent>()
            .FindAsync([delivery.EventId], cancellationToken);

        if (subscription is null || @event is null || !subscription.IsActive)
        {
            delivery.State = WebhookDeliveryState.Exhausted;
            delivery.LastError = "subscription or event no longer available";
            delivery.NextAttemptAtUtc = null;
            delivery.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var plan = await routePlanner.PlanAsync(new Uri(subscription.Url), cancellationToken);
        if (!plan.Allowed || plan.Address is null)
        {
            if (TransientDeliveryBlockReasons.IsTransient(plan.BlockedReason))
            {
                // Unresolvable destinations are usually temporary: keep the
                // delivery inside the normal retry budget instead of blocking.
                delivery.AttemptCount++;
                AppendAttempt(delivery, DateTime.UtcNow, null, plan.BlockedReason);
                delivery.State = delivery.AttemptCount >= options.Value.Webhooks.GetMaxAttempts()
                    ? WebhookDeliveryState.Exhausted
                    : WebhookDeliveryState.Pending;
                delivery.LastError = plan.BlockedReason;
                delivery.NextAttemptAtUtc = delivery.State == WebhookDeliveryState.Pending
                    ? DateTime.UtcNow.Add(ComputeBackoff(delivery.AttemptCount, options.Value.Webhooks.GetBaseRetryDelaySeconds()))
                    : null;
                delivery.LeaseOwner = null;
                delivery.LeaseExpiresAtUtc = null;
                delivery.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            // Policy violation: blocked before any bytes leave the host.
            delivery.State = WebhookDeliveryState.Blocked;
            delivery.BlockedReason = plan.BlockedReason;
            delivery.LastError = null;
            delivery.NextAttemptAtUtc = null;
            AppendAttempt(delivery, DateTime.UtcNow, null, $"blocked:{plan.BlockedReason}");
            delivery.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var pinnedAddress = plan.Address;
        var body = Encoding.UTF8.GetBytes(@event.PayloadJson);
        var timestamp = WebhookSigner.CurrentTimestamp();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WebhookSigner.EventIdHeaderName] = @event.Id.ToString(),
            [WebhookSigner.EventTypeHeaderName] = @event.Type,
            [WebhookSigner.DeliveryHeaderName] = delivery.Id.ToString(),
            [WebhookSigner.SignatureHeaderName] = WebhookSigner.Sign(
                subscription.Secret, @event.Id.ToString(), timestamp, @event.PayloadJson),
        };

        var outcome = await sender.SendAsync(
            plan.Url,
            pinnedAddress,
            headers,
            body,
            TimeSpan.FromSeconds(options.Value.Webhooks.GetTimeoutSeconds()),
            cancellationToken);

        delivery.AttemptCount++;
        delivery.LastStatusCode = outcome.StatusCode;
        delivery.LastError = outcome.Error;
        delivery.UpdatedAtUtc = DateTime.UtcNow;

        if (outcome.Success)
        {
            delivery.State = WebhookDeliveryState.Delivered;
            delivery.DeliveredAtUtc = DateTime.UtcNow;
            delivery.NextAttemptAtUtc = null;
        }
        else if (delivery.AttemptCount >= options.Value.Webhooks.GetMaxAttempts())
        {
            delivery.State = WebhookDeliveryState.Exhausted;
            delivery.NextAttemptAtUtc = null;
        }
        else
        {
            delivery.State = WebhookDeliveryState.Pending;
            delivery.NextAttemptAtUtc =
                DateTime.UtcNow.Add(ComputeBackoff(delivery.AttemptCount, options.Value.Webhooks.GetBaseRetryDelaySeconds()));
        }

        AppendAttempt(delivery, DateTime.UtcNow, outcome.StatusCode, outcome.Error);
        delivery.LeaseOwner = null;
        delivery.LeaseExpiresAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);
        Log.Attempted(_logger, delivery.Id, delivery.State, outcome.StatusCode);
    }

    private static void AppendAttempt(WebhookDelivery delivery, DateTime atUtc, int? statusCode, string? error)
    {
        List<DeliveryAttempt> attempts;
        try
        {
            attempts = System.Text.Json.JsonSerializer.Deserialize<List<DeliveryAttempt>>(delivery.AttemptLog, _jsonOptions) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            attempts = [];
        }

        attempts.Add(new DeliveryAttempt(atUtc, statusCode, error));
        if (attempts.Count > 5)
        {
            attempts.RemoveRange(0, attempts.Count - 5);
        }

        delivery.AttemptLog = System.Text.Json.JsonSerializer.Serialize(attempts, _jsonOptions);
    }

    /// <summary>Owner-scoped manual retry of a terminal or delayed delivery.</summary>
    public async Task<bool> RetryNowAsync(Guid ownerId, Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var delivery = await db.Set<WebhookDelivery>()
            .SingleOrDefaultAsync(x => x.Id == deliveryId && x.OwnerId == ownerId, cancellationToken);
        if (delivery is null || delivery.State == WebhookDeliveryState.Delivering)
        {
            return false;
        }

        delivery.State = WebhookDeliveryState.Pending;
        delivery.AttemptCount = 0;
        delivery.NextAttemptAtUtc = DateTime.UtcNow;
        delivery.BlockedReason = null;
        delivery.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Owner-scoped secret rotation: returns the new clear secret exactly once;
    /// URL and event types are untouched and later deliveries sign with the new
    /// secret. Returns null when the subscription does not belong to the owner.
    /// </summary>
    public async Task<string?> RotateSecretAsync(
        Guid ownerId,
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await db.Set<WebhookSubscription>()
            .SingleOrDefaultAsync(x => x.Id == subscriptionId && x.OwnerId == ownerId, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var secret = $"whsec_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=')}";
        subscription.Secret = secret;
        await db.SaveChangesAsync(cancellationToken);
        return secret;
    }

    /// <summary>Removes terminal deliveries past the retention window.</summary>
    public async Task<int> ApplyRetentionAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var cutoff = nowUtc.AddDays(-options.Value.Webhooks.GetRetentionDays());
        return await db.Set<WebhookDelivery>()
            .Where(x => x.State != WebhookDeliveryState.Pending
                        && x.State != WebhookDeliveryState.Delivering
                        && x.UpdatedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<List<WebhookDeliveryView>> ListDeliveriesAsync(
        Guid ownerId,
        Guid? subscriptionId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var query = db.Set<WebhookDelivery>().AsNoTracking().Where(x => x.OwnerId == ownerId);
        if (subscriptionId is Guid filter)
        {
            query = query.Where(x => x.SubscriptionId == filter);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.SubscriptionId,
                x.EventId,
                State = x.State.ToString(),
                x.AttemptCount,
                x.LastStatusCode,
                x.LastError,
                x.BlockedReason,
                x.CreatedAtUtc,
                x.NextAttemptAtUtc,
                x.DeliveredAtUtc,
                x.AttemptLog,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new WebhookDeliveryView(
            x.Id,
            x.SubscriptionId,
            x.EventId,
            x.State,
            x.AttemptCount,
            x.LastStatusCode,
            x.LastError,
            x.BlockedReason,
            x.CreatedAtUtc,
            x.NextAttemptAtUtc,
            x.DeliveredAtUtc,
            ParseAttempts(x.AttemptLog))).ToList();
    }

    private static List<DeliveryAttempt> ParseAttempts(string attemptLog)
    {
        try
        {
            var attempts = System.Text.Json.JsonSerializer.Deserialize<List<DeliveryAttempt>>(attemptLog, _jsonOptions);
            return attempts ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public static TimeSpan ComputeBackoff(int attemptJustMade, int baseDelaySeconds)
    {
        // Bounded exponential backoff: base * 2^(attempt-1), capped at 1 hour.
        var exponent = Math.Min(attemptJustMade - 1, 12);
        var seconds = (long)Math.Min((double)baseDelaySeconds * (1L << exponent), 3600);
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool SubscribesTo(WebhookSubscription subscription, string eventType)
    {
        return AutomationScopes.Parse(subscription.EventTypes).Contains(eventType);
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Webhook delivery {DeliveryId} attempted -> {State} ({StatusCode}).")]
        public static partial void Attempted(
            ILogger logger, Guid deliveryId, WebhookDeliveryState state, int? statusCode);
    }
}
