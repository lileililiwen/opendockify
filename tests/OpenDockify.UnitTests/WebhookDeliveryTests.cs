using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenDockify.Integrations.Configuration;
using OpenDockify.Integrations.Models;
using OpenDockify.Integrations.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class WebhookSignatureTests
{
    [Fact]
    public void Signature_covers_event_id_timestamp_and_exact_body()
    {
        const string secret = "whsec_test_secret";
        const string eventId = "5f0c0e11-1111-2222-3333-444455556666";
        const long timestamp = 1_700_000_000;
        const string body = """{"type":"document.finalized"}""";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(
            Encoding.UTF8.GetBytes($"{eventId}.{timestamp}.{body}"))).ToLowerInvariant();

        var header = WebhookSigner.Sign(secret, eventId, timestamp, body);

        Assert.Equal($"t={timestamp}, v1={expected}", header);
        Assert.True(WebhookSigner.Verify(secret, eventId, timestamp, body, header, maxAgeSeconds: 300, nowUnixSeconds: timestamp));
    }

    [Fact]
    public void Verification_rejects_tampering_and_stale_timestamps()
    {
        const string secret = "whsec_test_secret";
        var header = WebhookSigner.Sign(secret, "event", 1_700_000_000, "body");

        Assert.False(WebhookSigner.Verify(secret, "event", 1_700_000_000, "tampered", header, 300, 1_700_000_000));
        Assert.False(WebhookSigner.Verify("whsec_other", "event", 1_700_000_000, "body", header, 300, 1_700_000_000));
        Assert.False(WebhookSigner.Verify(secret, "other-event", 1_700_000_000, "body", header, 300, 1_700_000_000));
        Assert.False(WebhookSigner.Verify(secret, "event", 1_700_000_000, "body", header, 300, 1_700_000_301));
        Assert.False(WebhookSigner.Verify(secret, "event", 1_700_000_000, "body", "t=nope,v1=nope", 300, 1_700_000_000));
    }
}

public sealed class SsrfGuardTests
{
    private static WebhookRoutePlanner Planner(params string[] allowlist)
    {
        return new(allowlist, new FakeDnsResolver());
    }

    [Theory]
    [InlineData("127.0.0.1", "loopback")]
    [InlineData("10.9.9.9", "private")]
    [InlineData("172.16.0.5", "private")]
    [InlineData("192.168.1.1", "private")]
    [InlineData("169.254.169.254", "link-local")]
    [InlineData("224.0.0.1", "multicast")]
    [InlineData("0.0.0.0", "unspecified")]
    [InlineData("100.64.0.1", "shared-address-space")]
    public async Task Prohibited_destinations_are_blocked_with_a_reason(string ip, string reason)
    {
        var result = await Planner().PlanAsync(new Uri($"https://{ip}/hook"));

        Assert.False(result.Allowed);
        Assert.Equal(reason, result.BlockedReason);
        Assert.Null(result.Address);
    }

    [Fact]
    public async Task Public_destinations_resolve_to_a_pinned_address()
    {
        var result = await Planner().PlanAsync(new Uri("https://api.example.com/hook"));

        Assert.True(result.Allowed);
        Assert.Equal(IPAddress.Parse("93.184.216.34"), result.Address);
    }

    [Fact]
    public async Task Ipv4_mapped_ipv6_loopback_is_blocked()
    {
        var planner = new WebhookRoutePlanner(
            [], new FakeDnsResolver().Map("rebind.example.com", "::ffff:127.0.0.1"));
        var result = await planner.PlanAsync(new Uri("https://rebind.example.com/hook"));

        Assert.False(result.Allowed);
        Assert.Equal("loopback", result.BlockedReason);
    }

    [Fact]
    public async Task Plain_http_is_rejected()
    {
        var result = await Planner().PlanAsync(new Uri("http://api.example.com/hook"));

        Assert.False(result.Allowed);
        Assert.Equal("scheme", result.BlockedReason);
    }

    [Fact]
    public async Task Deployer_allowlist_cidr_permits_private_ranges()
    {
        var result = await Planner("10.0.0.0/8").PlanAsync(new Uri("https://10.1.2.3/hook"));

        Assert.True(result.Allowed);
        Assert.Equal(IPAddress.Parse("10.1.2.3"), result.Address);
    }

    [Fact]
    public async Task Deployer_allowlisted_hostnames_skip_address_prohibition()
    {
        var planner = new WebhookRoutePlanner(
            ["metrics.internal"],
            new FakeDnsResolver().Map("metrics.internal", "192.168.0.10"));
        var result = await planner.PlanAsync(new Uri("https://metrics.internal/hook"));

        Assert.True(result.Allowed);
        Assert.Equal(IPAddress.Parse("192.168.0.10"), result.Address);
    }

    [Fact]
    public async Task Unresolvable_hosts_are_blocked()
    {
        var planner = new WebhookRoutePlanner([], new FakeDnsResolver());
        var result = await planner.PlanAsync(new Uri("https://missing.invalid/hook"));

        Assert.False(result.Allowed);
        Assert.Equal("dns-resolution-failed", result.BlockedReason);
    }
}

public sealed class WebhookDeliveryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestIntegrationsDbContext _db;
    private readonly RecordingSender _sender = new();
    private readonly IntegrationsOptions _options = new()
    {
        Webhooks = new WebhookOptions
        {
            Enabled = true,
            MaxAttempts = 3,
            BaseRetryDelaySeconds = 60,
        },
    };

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly WebhookDeliveryService _service;

    public WebhookDeliveryServiceTests()
    {
        _connection = AutomationTestHarness.OpenConnection(AutomationTestHarness.NewDatabaseName());
        _db = AutomationTestHarness.CreateContext(_connection);
        _db.Database.EnsureCreated();
        _service = new WebhookDeliveryService(
            _db,
            new WebhookRoutePlanner([], new FakeDnsResolver()),
            _sender,
            Options.Create(_options));
    }

    [Fact]
    public async Task Successful_delivery_records_state_signature_headers_and_body()
    {
        var (delivery, subscription, @event) = await SeedDeliveryAsync();

        await _service.DeliverAsync(delivery);

        Assert.Equal(WebhookDeliveryState.Delivered, delivery.State);
        Assert.NotNull(delivery.DeliveredAtUtc);
        var attempt = Assert.Single(_sender.Attempts);
        Assert.Equal(subscription.Url, attempt.Url.ToString());
        Assert.Equal(@event.PayloadJson, Encoding.UTF8.GetString(attempt.Body));

        var signature = attempt.Headers["X-OpenDockify-Signature"];
        var timestampPart = signature.Split(',', StringSplitOptions.TrimEntries)[0];
        var t = long.Parse(timestampPart.AsSpan(2), CultureInfo.InvariantCulture);
        Assert.True(WebhookSigner.Verify(
            subscription.Secret,
            @event.Id.ToString(),
            t,
            @event.PayloadJson,
            signature,
            maxAgeSeconds: 600,
            nowUnixSeconds: t));
    }

    [Fact]
    public async Task Failures_back_off_exponentially_then_exhaust()
    {
        var (delivery, _, _) = await SeedDeliveryAsync();
        _sender.RespondWith(() => new WebhookSendOutcome(false, 500, "server error"));

        await _service.DeliverAsync(delivery);
        Assert.Equal(WebhookDeliveryState.Pending, delivery.State);
        Assert.Equal(1, delivery.AttemptCount);
        var firstDelay = delivery.NextAttemptAtUtc.GetValueOrDefault() - DateTime.UtcNow;
        Assert.InRange(firstDelay.TotalSeconds, 55, 65);

        // Simulate the scheduler waiting out the backoff window.
        delivery.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await _service.DeliverAsync(delivery);
        Assert.Equal(2, delivery.AttemptCount);
        var secondDelay = delivery.NextAttemptAtUtc.GetValueOrDefault() - DateTime.UtcNow;
        Assert.InRange(secondDelay.TotalMinutes, 1.9, 2.1);

        delivery.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-3);
        await _service.DeliverAsync(delivery);
        Assert.Equal(WebhookDeliveryState.Exhausted, delivery.State);
        Assert.Equal(3, delivery.AttemptCount);
        Assert.Null(delivery.NextAttemptAtUtc == null ? null : delivery.NextAttemptAtUtc);
        Assert.Equal(500, delivery.LastStatusCode);
    }

    [Fact]
    public async Task Expired_leases_are_reclaimed_but_active_leases_are_not()
    {
        var (stuck, _, _) = await SeedDeliveryAsync();
        stuck.State = WebhookDeliveryState.Delivering;
        stuck.LeaseOwner = Guid.NewGuid();
        stuck.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        var claimed = await _service.ClaimDueDeliveriesAsync(batchSize: 10);
        Assert.Contains(claimed, x => x.Id == stuck.Id);
        Assert.NotEqual(stuck.LeaseExpiresAtUtc, DateTime.UtcNow);

        var (active, _, _) = await SeedDeliveryAsync();
        active.State = WebhookDeliveryState.Delivering;
        active.LeaseOwner = Guid.NewGuid();
        active.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
        await _db.SaveChangesAsync();

        claimed = await _service.ClaimDueDeliveriesAsync(batchSize: 10);
        Assert.DoesNotContain(claimed, x => x.Id == active.Id);
    }

    [Fact]
    public async Task Prohibited_destination_is_blocked_without_contacting_it()
    {
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerId,
            Url = "https://169.254.169.254/latest/meta-data",
            Secret = "whsec_blocked",
            EventTypes = AutomationEvents.DocumentFinalized,
        };
        var @event = SeedEvent(subscription.OwnerId);
        _db.WebhookSubscriptions.Add(subscription);
        _db.OutboxEvents.Add(@event);
        await _db.SaveChangesAsync();
        await _service.EnqueuePendingEventsAsync();
        var delivery = await _db.WebhookDeliveries.SingleAsync();

        await _service.DeliverAsync(delivery);

        Assert.Equal(WebhookDeliveryState.Blocked, delivery.State);
        Assert.Equal("link-local", delivery.BlockedReason);
        Assert.Empty(_sender.Attempts);
    }

    [Fact]
    public async Task Manual_retry_resets_terminal_deliveries_for_the_owner_only()
    {
        var (delivery, _, _) = await SeedDeliveryAsync();
        delivery.State = WebhookDeliveryState.Exhausted;
        delivery.AttemptCount = 3;
        delivery.NextAttemptAtUtc = DateTime.UtcNow.AddHours(2);
        await _db.SaveChangesAsync();

        Assert.False(await _service.RetryNowAsync(Guid.NewGuid(), delivery.Id));
        Assert.True(await _service.RetryNowAsync(_ownerId, delivery.Id));
        Assert.Equal(WebhookDeliveryState.Pending, delivery.State);
        Assert.Equal(0, delivery.AttemptCount);
        Assert.True(delivery.NextAttemptAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Retention_purges_only_expired_terminal_deliveries()
    {
        var (oldDelivered, _, _) = await SeedDeliveryAsync();
        oldDelivered.State = WebhookDeliveryState.Delivered;
        oldDelivered.DeliveredAtUtc = DateTime.UtcNow.AddDays(-31);
        oldDelivered.UpdatedAtUtc = DateTime.UtcNow.AddDays(-31);

        var (recentPending, _, _) = await SeedDeliveryAsync();
        recentPending.State = WebhookDeliveryState.Pending;

        var removed = await _service.ApplyRetentionAsync(DateTime.UtcNow);

        Assert.Equal(1, removed);
        // ExecuteDelete bypasses the change tracker: verify against the database.
        Assert.False(await _db.WebhookDeliveries.AsNoTracking().AnyAsync(x => x.Id == oldDelivered.Id));
        Assert.True(await _db.WebhookDeliveries.AsNoTracking().AnyAsync(x => x.Id == recentPending.Id));
    }

    [Fact]
    public async Task Outbox_events_fan_out_once_per_active_subscription()
    {
        var matching = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerId,
            Url = "https://hooks.example.com/a",
            Secret = "s1",
            EventTypes = AutomationEvents.DocumentFinalized,
            IsActive = true,
        };
        var otherType = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerId,
            Url = "https://hooks.example.com/b",
            Secret = "s2",
            EventTypes = "document.deleted",
            IsActive = true,
        };
        var inactive = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerId,
            Url = "https://hooks.example.com/c",
            Secret = "s3",
            EventTypes = AutomationEvents.DocumentFinalized,
            IsActive = false,
        };
        _db.WebhookSubscriptions.AddRange(matching, otherType, inactive);
        _db.OutboxEvents.Add(SeedEvent(_ownerId));
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _service.EnqueuePendingEventsAsync());
        Assert.Equal(0, await _service.EnqueuePendingEventsAsync());
        Assert.Equal(1, await _db.WebhookDeliveries.CountAsync());
        Assert.All(await _db.OutboxEvents.ToListAsync(), x => Assert.NotNull(x.ProcessedAtUtc));
    }

    private static OutboxEvent SeedEvent(Guid ownerId)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Type = AutomationEvents.DocumentFinalizedName,
            PayloadJson = JsonSerializer.Serialize(
            new
            {
                eventId = Guid.NewGuid(),
                type = AutomationEvents.DocumentFinalizedName,
                data = new { documentId = Guid.NewGuid() },
            },
            AutomationTestHarness.SerializerOptions),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    private async Task<(WebhookDelivery Delivery, WebhookSubscription Subscription, OutboxEvent Event)> SeedDeliveryAsync()
    {
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerId,
            Url = "https://hooks.example.com/receiver",
            Secret = "whsec_seed_secret",
            EventTypes = AutomationEvents.DocumentFinalized,
            IsActive = true,
        };
        var @event = SeedEvent(_ownerId);
        _db.WebhookSubscriptions.Add(subscription);
        _db.OutboxEvents.Add(@event);
        await _db.SaveChangesAsync();
        await _service.EnqueuePendingEventsAsync();
        var delivery = await _db.WebhookDeliveries.SingleAsync(x => x.SubscriptionId == subscription.Id);
        return (delivery, subscription, @event);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private sealed class RecordingSender : IWebhookSender
    {
        public List<WebhookAttempt> Attempts { get; } = [];

        private Func<WebhookSendOutcome>? _responder;

        public void RespondWith(Func<WebhookSendOutcome> responder)
        {
            _responder = responder;
        }

        public Task<WebhookSendOutcome> SendAsync(
            Uri url, IPAddress pinnedAddress, IReadOnlyDictionary<string, string> headers,
            byte[] body, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Attempts.Add(new WebhookAttempt(url, headers, body));
            return Task.FromResult(_responder is null
                ? new WebhookSendOutcome(true, 200, null)
                : _responder());
        }
    }

    private sealed record WebhookAttempt(
        Uri Url, IReadOnlyDictionary<string, string> Headers, byte[] Body);
}

public sealed class FakeDnsResolver : IDnsResolver
{
    private readonly Dictionary<string, IPAddress[]> _map = new(StringComparer.OrdinalIgnoreCase);

    public FakeDnsResolver Map(string host, params string[] addresses)
    {
        _map[host] = addresses.Select(IPAddress.Parse).ToArray();
        return this;
    }

    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (_map.TryGetValue(host, out var addresses))
        {
            return Task.FromResult(addresses);
        }

        if (IPAddress.TryParse(host, out var literal))
        {
            return Task.FromResult(new[] { literal });
        }

        // Default: a plausible public address for any other host.
        return host.EndsWith(".example.com", StringComparison.OrdinalIgnoreCase)
            ? Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") })
            : Task.FromResult<IPAddress[]>([]);
    }
}
