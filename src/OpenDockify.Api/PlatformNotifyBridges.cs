using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Core.Time;
using Platform.Eventing.Contracts;
using Platform.Idempotency;
using Platform.Idempotency.DependencyInjection;
using Platform.Jobs;
using Platform.Quota.AspNetCore.Contracts;
using Platform.Quota.AspNetCore.DependencyInjection;
using Platform.Quota.DependencyInjection;
using Platform.RateLimiting.DependencyInjection;
using Platform.Webhooks.AspNetCore.DependencyInjection;
using Platform.Webhooks.Contracts.Common;
using Platform.Webhooks.Contracts.DependencyInjection;
using Platform.Webhooks.Contracts.Outbound;
using Platform.Webhooks.Contracts.Security;

namespace OpenDockify.Api;

/// <summary>
/// Platform idempotency bridge for automation finalize: fingerprint match
/// replays the identical stored response without creating a second document.
/// Keyed by token+key+route so per-token isolation is preserved.
/// </summary>
public sealed class PlatformIdempotencyBridge(IIdempotencyStore store, Platform.Core.Time.IClock clock)
{
    public static string Key(Guid tokenId, string key, string route)
    {
        return $"{tokenId:N}:{key}:{route}";
    }

    public async Task<(bool Hit, string? Body, int Status)> TryReplayAsync(Guid tokenId, string key, string route, string digest, CancellationToken ct)
    {
        var record = await store.TryGetAsync(Key(tokenId, key, route), ct);
        if (record is null)
        {
            return (false, null, 0);
        }

        if (!string.Equals(record.Fingerprint, digest, StringComparison.Ordinal))
        {
            return (false, null, 409);
        }

        return (true, record.ResponseBody, record.StatusCode);
    }

    public Task SaveAsync(Guid tokenId, string key, string route, string digest, int status, string body, CancellationToken ct)
    {
        return store.SaveAsync(new Platform.Idempotency.IdempotencyRecord(Key(tokenId, key, route), digest, status, "application/json", body, clock.UtcNow), ct);
    }
}

/// <summary>
/// Platform outbox bridge: mirrors automation finalize events into the
/// platform durable outbox (in-memory default) alongside the bespoke
/// OutboxEvents table.
/// </summary>
public sealed class PlatformOutboxBridge(IOutboxStore outbox, Platform.Core.Time.IClock clock)
{
    public Task PublishFinalizedAsync(Guid eventId, Guid ownerId, string payloadJson, string? correlationId, CancellationToken ct)
    {
        var envelope = new DurableEventEnvelope(
            eventId.ToString("N"),
            "document-finalized",
            payloadJson,
            clock.UtcNow,
            ownerId.ToString("N"),
            correlationId);
        return outbox.AddAsync(OutboxMessage.Create(envelope, clock.UtcNow), ct);
    }
}

/// <summary>Platform webhook secret resolver backed by existing subscriptions.</summary>
public sealed class OpenDockifyWebhookSecretResolver(DbContext db) : IWebhookSigningSecretResolver
{
    public byte[]? ResolveSecret(string secretKey)
    {
        var sub = db.Set<OpenDockify.Integrations.Models.WebhookSubscription>().AsNoTracking().FirstOrDefault(x => x.Id.ToString() == secretKey);
        if (sub is null)
        {
            return null;
        }

        return Encoding.UTF8.GetBytes(sub.Secret);
    }
}

/// <summary>Platform subscription store adapter over existing subscriptions.</summary>
public sealed class OpenDockifyWebhookSubscriptionStore(DbContext db) : IWebhookSubscriptionStore
{
    public Task<Platform.Webhooks.Contracts.Outbound.WebhookSubscription?> GetAsync(WebhookSubscriptionId id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id.Value, out var guid))
        {
            return Task.FromResult<Platform.Webhooks.Contracts.Outbound.WebhookSubscription?>(null);
        }

        var row = db.Set<OpenDockify.Integrations.Models.WebhookSubscription>().AsNoTracking().FirstOrDefault(x => x.Id == guid);
        if (row is null)
        {
            return Task.FromResult<Platform.Webhooks.Contracts.Outbound.WebhookSubscription?>(null);
        }

        var events = (row.EventTypes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Task.FromResult<Platform.Webhooks.Contracts.Outbound.WebhookSubscription?>(Platform.Webhooks.Contracts.Outbound.WebhookSubscription.Create(
            new WebhookSubscriptionId(row.Id.ToString("N")),
            new Uri(row.Url),
            row.Id.ToString("N"),
            events,
            row.IsActive,
            new WebhookRetryPolicy(5, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(15))));
    }

    public Task<IReadOnlyList<Platform.Webhooks.Contracts.Outbound.WebhookSubscription>> ListEnabledAsync(string eventType, CancellationToken cancellationToken = default)
    {
        var rows = db.Set<OpenDockify.Integrations.Models.WebhookSubscription>().AsNoTracking().Where(x => x.IsActive).ToList();
        var list = new List<Platform.Webhooks.Contracts.Outbound.WebhookSubscription>();
        foreach (var row in rows)
        {
            var events = (row.EventTypes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (events.Length != 0 && !events.Contains(eventType, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            list.Add(Platform.Webhooks.Contracts.Outbound.WebhookSubscription.Create(
                new WebhookSubscriptionId(row.Id.ToString("N")),
                new Uri(row.Url),
                row.Id.ToString("N"),
                events,
                true,
                new WebhookRetryPolicy(5, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(15))));
        }

        return Task.FromResult<IReadOnlyList<Platform.Webhooks.Contracts.Outbound.WebhookSubscription>>(list);
    }
}

/// <summary>
/// Recurring job that drains the platform outbox through the platform
/// webhook dispatcher with HMAC retry. Runs alongside the bespoke
/// WebhookDeliveryJobHandler; no-op when webhooks are disabled.
/// </summary>
[RecurringJob("*/1 * * * *", Name = "openDockify.platform.webhookDispatch", TimeZone = "UTC")]
public sealed partial class PlatformWebhookDispatchJobHandler(
    IServiceScopeFactory scopes,
    IOptions<OpenDockify.Integrations.Configuration.IntegrationsOptions> options,
    ILogger<PlatformWebhookDispatchJobHandler> logger) : IRecurringJobHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Webhooks.Enabled)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IWebhookOutboundDispatcher>();
        var subs = scope.ServiceProvider.GetRequiredService<IWebhookSubscriptionStore>();
        var platformClock = scope.ServiceProvider.GetRequiredService<Platform.Core.Time.IClock>();
        var claimed = await outbox.ClaimAsync(platformClock.UtcNow, "platform-webhook", TimeSpan.FromMinutes(5), 20, cancellationToken);
        foreach (var msg in claimed)
        {
            try
            {
                var targets = await subs.ListEnabledAsync(msg.Envelope.PayloadType, cancellationToken);
#pragma warning disable S3267 // dispatch has side effects per subscription; not a projection.
                foreach (var sub in targets)
                {
                    // Payload versioned v1; signed t,v1 HMAC by the platform dispatcher; never logs the secret.
                    var result = await dispatcher.DispatchAsync(sub.Id, "v1." + msg.Envelope.PayloadType, msg.Envelope.MessageId, msg.Envelope.PayloadJson, cancellationToken);
                    if (result.Outcome == WebhookDeliveryOutcome.Succeeded)
                    {
                        Log.Dispatched(logger, msg.Envelope.MessageId, sub.Id.Value);
                    }
                }
#pragma warning restore S3267

                await outbox.MarkSucceededAsync(msg.MessageId, "platform-webhook", cancellationToken);
            }
            catch (Exception ex)
            {
                await outbox.MarkFailedAsync(msg.MessageId, "platform-webhook", new DurableDispatchFailure("webhook.dispatch_failed", "Platform webhook dispatch failed.", false), cancellationToken);
                Log.Failed(logger, ex, msg.MessageId);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Platform webhook dispatched event={Event} sub={Sub}.")]
        public static partial void Dispatched(ILogger logger, string @event, string sub);

        [LoggerMessage(2, LogLevel.Error, "Platform webhook dispatch failed event={Event}.")]
        public static partial void Failed(ILogger logger, Exception ex, string @event);
    }
}

/// <summary>PBKDF2 link-password hashing with constant-time verify.</summary>
public static class ShareLinkPassword
{
    private const int _iterations = 100_000;
    private const int _saltBytes = 16;
    private const int _hashBytes = 32;

    public static (byte[] Hash, byte[] Salt) HashNew(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_saltBytes);
        return (Compute(password, salt), salt);
    }

    public static byte[] Compute(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, _iterations, HashAlgorithmName.SHA256, _hashBytes);
    }

    public static bool Verify(string password, byte[] hash, byte[] salt)
    {
        var computed = Compute(password, salt);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }
}

public static class PlatformNotifyWiring
{
    public static IServiceCollection AddPlatformNotifyRateQuota(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPlatformRateLimiting(o =>
        {
            configuration.GetSection(Platform.RateLimiting.RateLimitingOptions.SectionName).Bind(o);
            o.Policies = new Platform.RateLimiting.RateLimitPolicies(new[]
            {
                new Platform.RateLimiting.RateLimitPolicyOptions { Name = "generate", Limit = 60, WindowSeconds = 60 },
                new Platform.RateLimiting.RateLimitPolicyOptions { Name = "preview", Limit = 60, WindowSeconds = 60 },
                new Platform.RateLimiting.RateLimitPolicyOptions { Name = "finalize", Limit = 30, WindowSeconds = 60 },
                new Platform.RateLimiting.RateLimitPolicyOptions { Name = "ai-polish", Limit = 20, WindowSeconds = 60 },
            });
        });
        services.AddPlatformQuota();
        services.AddPlatformQuotaAspNetCore(o =>
        {
            o.Enabled = true;
            o.MissingContextPolicy = MissingContextPolicy.Allow;
            o.ProviderUnavailablePolicy = QuotaUnavailablePolicy.Allow;
            o.ExemptPathPrefixes.Clear();
            o.ExemptPathPrefixes.Add("/health");
            o.ExemptPathPrefixes.Add("/healthz");
            o.ExemptPathPrefixes.Add("/readyz");
            o.ExemptPathPrefixes.Add("/openapi");
            o.ExemptPathPrefixes.Add("/api/v1/automation/openapi.json");
        });
        services.AddSingleton<IQuotaSubjectResolver, NotifyQuotaSubjectResolver>();
        services.AddSingleton<IQuotaResourceResolver, NotifyQuotaResourceResolver>();
        services.AddSingleton<NotifyRateGate>();
        services.AddPlatformIdempotency(o =>
        {
            configuration.GetSection(Platform.Idempotency.IdempotencyOptions.SectionName).Bind(o);
            o.Enabled = true;
            o.RetentionSeconds = 86400;
        });
        services.AddSingleton<IOutboxStore>(sp =>
            new InMemoryOutboxStore(sp.GetRequiredService<Platform.Core.Time.IClock>(), new DurableEventingOptions()));
        services.AddSingleton<PlatformIdempotencyBridge>();
        services.AddSingleton<PlatformOutboxBridge>();
        services.AddPlatformWebhooks(o => configuration.GetSection(WebhookOptions.SectionName).Bind(o));
        services.AddPlatformWebhooksAspNetCore();
        services.AddScoped<IWebhookSubscriptionStore, OpenDockifyWebhookSubscriptionStore>();
        services.AddScoped<IWebhookSigningSecretResolver, OpenDockifyWebhookSecretResolver>();
        services.AddSingleton<IWebhookDeliveryStore, InMemoryWebhookDeliveryStore>();
        services.AddScoped<PlatformWebhookDispatchJobHandler>();
        return services;
    }
}
