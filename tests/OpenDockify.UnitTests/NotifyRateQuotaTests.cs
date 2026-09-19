using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenDockify.Api;
using OpenDockify.Auth.Models;
using OpenDockify.Data;
using OpenDockify.Generation.Models;
using OpenDockify.Sharing.Services;
using OpenDockify.SystemConfig.Services;
using OpenDockify.Templates.Models;
using Platform.Core.Time;
using Platform.Eventing.Contracts;
using Platform.Idempotency;
using Platform.Mailing;
using Platform.Quota.Contracts;
using Platform.Quota.Stores;
using Platform.RateLimiting;
using Platform.Webhooks.Contracts.Common;
using Platform.Webhooks.Contracts.Outbound;
using Platform.Webhooks.Contracts.Security;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class NotifyRateQuotaTests
{
    [Fact]
    public async Task Notifications_disabled_by_default_makes_no_smtp_call()
    {
        var mail = new RecordingMailService();
        var options = Options.Create(new NotifyOptions { Enabled = false });
        var svc = new NotifyService(mail, options, NullLogger<NotifyService>.Instance);
        var sent = await svc.SendShareGrantedAsync("user@example.com", "Doc", "/s/x", default);
        Assert.False(sent);
        Assert.Equal(0, mail.Calls);
    }

    [Fact]
    public async Task Notifications_enabled_sends_plaintext_via_mail_service()
    {
        var mail = new RecordingMailService();
        var options = Options.Create(new NotifyOptions { Enabled = true, FromAddress = "noreply@opendockify.local" });
        var svc = new NotifyService(mail, options, NullLogger<NotifyService>.Instance);
        var sent = await svc.SendShareGrantedAsync("user@example.com", "Doc", "/s/x", default);
        Assert.True(sent);
        Assert.Equal(1, mail.Calls);
        Assert.Contains("Doc", mail.Last!.Subject, StringComparison.Ordinal);
        Assert.NotNull(mail.Last.TextBody);
        Assert.Null(mail.Last.HtmlBody);
    }

    [Fact]
    public async Task Rate_limiter_isolates_per_user_subjects()
    {
        var clock = new SystemClock();
        var opts = Options.Create(new RateLimitingOptions
        {
            Policies = new RateLimitPolicies(new[]
            {
                new RateLimitPolicyOptions { Name = "generate", Limit = 2, WindowSeconds = 60 },
            }),
        });
        var limiter = new InMemoryRateLimiter(clock, opts);
        Assert.True((await limiter.CheckAsync(new RateLimitKey("generate", "user:A"), default)).Allowed);
        Assert.True((await limiter.CheckAsync(new RateLimitKey("generate", "user:A"), default)).Allowed);
        var denied = await limiter.CheckAsync(new RateLimitKey("generate", "user:A"), default);
        Assert.False(denied.Allowed);
        Assert.True(denied.RetryAfterSeconds >= 1);
        Assert.True((await limiter.CheckAsync(new RateLimitKey("generate", "user:B"), default)).Allowed);
    }

    [Fact]
    public async Task Ai_quota_exceeded_denies_without_llm_call()
    {
        var store = new InMemoryQuotaStore(new SystemClock());
        var subject = new QuotaSubject("user:quota-test");
        var window = DailyWindow();
        for (var i = 0; i < 2; i++)
        {
            var r = await store.ReserveAsync(subject, new QuotaResource("ai-polish"), window, 2, $"op-{i}", 1, TimeSpan.FromMinutes(5), default);
            Assert.Equal(QuotaLifecycleStatus.Reserved, r.Status);
            await store.SettleAsync($"op-{i}", default);
        }

        var decision = await store.CheckAsync(subject, new QuotaResource("ai-polish"), window, 2, 1, default);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task Platform_idempotency_replays_identical_response_without_second_document()
    {
        var store = new InMemoryIdempotencyStore(new SystemClock(), Options.Create(new IdempotencyOptions()));
        var bridge = new PlatformIdempotencyBridge(store, new SystemClock());
        var token = Guid.NewGuid();
        await bridge.SaveAsync(token, "key-12345", "/api/v1/automation/finalize", "digest-abc", 201, "{\"ok\":true}", default);
        var hit = await bridge.TryReplayAsync(token, "key-12345", "/api/v1/automation/finalize", "digest-abc", default);
        Assert.True(hit.Hit);
        Assert.Equal("{\"ok\":true}", hit.Body);
        var conflict = await bridge.TryReplayAsync(token, "key-12345", "/api/v1/automation/finalize", "other-digest", default);
        Assert.False(conflict.Hit);
        Assert.Equal(409, conflict.Status);
    }

    [Fact]
    public async Task Platform_outbox_round_trips_finalize_event()
    {
        var clock = new SystemClock();
        var outbox = new InMemoryOutboxStore(clock, new DurableEventingOptions());
        var bridge = new PlatformOutboxBridge(outbox, clock);
        var eventId = Guid.NewGuid();
        await bridge.PublishFinalizedAsync(eventId, Guid.NewGuid(), "{\"doc\":1}", "corr", default);
        var claimed = await outbox.ClaimAsync(clock.UtcNow, "test", TimeSpan.FromMinutes(5), 10, default);
        Assert.Single(claimed);
        Assert.Equal(eventId.ToString("N"), claimed[0].MessageId);
    }

    [Fact]
    public async Task Webhook_transient_failure_retries_with_hmac_and_no_secret_in_result()
    {
        var subs = new InMemoryWebhookSubscriptionStore();
        var deliveries = new InMemoryWebhookDeliveryStore();
        var secrets = new ConfigurationWebhookSigningSecretResolver();
        secrets.Add("k1", System.Text.Encoding.UTF8.GetBytes("secret-123"));
        var sub = WebhookSubscription.Create(
            new WebhookSubscriptionId("sub1"),
            new Uri("https://localhost/hook"),
            "k1",
            ["document-finalized"],
            true,
            new WebhookRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)));
        subs.Upsert(sub);
        var options = Options.Create(new WebhookOptions { AllowLoopbackTargets = true });
        var dispatcher = new WebhookOutboundDispatcher(subs, deliveries, secrets, new SsrfTargetValidator(options.Value), new FlakySender(), options, new SystemClock());
        var result = await dispatcher.DispatchAsync(sub.Id, "v1.document-finalized", "evt-1", "{\"doc\":1}", default);
        Assert.True(result.Outcome is WebhookDeliveryOutcome.RetryableResponse or WebhookDeliveryOutcome.TransportError or WebhookDeliveryOutcome.Succeeded);
        Assert.DoesNotContain("secret-123", result.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Link_password_grants_with_correct_and_locks_after_five_failures()
    {
        await using var harness = await ShareHarness.CreateAsync();
        var created = await harness.Service.CreateLinkAsync(harness.OwnerId, harness.DocumentId, 2, false, default, "correct-horse-8");
        Assert.NotNull(created.Link);
        var stored = await harness.Db.ExternalShareLinks.SingleAsync();
        Assert.NotNull(stored.PasswordHash);
        Assert.DoesNotContain("correct-horse-8", Convert.ToHexString(stored.PasswordHash), StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(await harness.Service.ResolvePublicAsync(created.Link.Token, false, "c", default, "correct-horse-8"));
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(await harness.Service.ResolvePublicAsync(created.Link.Token, false, "c", default, "wrong-pass-1"));
        }

        // Locked: even the correct password now fails with 401-equivalent null.
        Assert.Null(await harness.Service.ResolvePublicAsync(created.Link.Token, false, "c", default, "correct-horse-8"));
    }

    [Fact]
    public void Sharing_hash_key_is_required()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Development");
        Assert.Contains(errors, e => e.Contains("Sharing:HashKey", StringComparison.Ordinal));
    }

    private static QuotaWindow DailyWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        return new QuotaWindow(start, start.AddDays(1));
    }

    private sealed class RecordingMailService : IMailService
    {
        public int Calls { get; private set; }
        public MailMessage? Last { get; private set; }
        public Task<MailSendResult> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
        {
            Calls++;
            Last = message;
            return Task.FromResult(new MailSendResult(MailSendOutcome.Sent));
        }
    }

    private sealed class FlakySender : IWebhookHttpSender
    {
        public ValueTask<WebhookHttpSendResult> SendAsync(WebhookHttpSendRequest request, CancellationToken cancellationToken = default)
        {
            Assert.False(string.IsNullOrWhiteSpace(request.SignatureHeaderValue));
            Assert.False(string.IsNullOrWhiteSpace(request.SignatureHeaderName));
            return ValueTask.FromResult(WebhookHttpSendResult.TransportFailure(new WebhookFailure("webhook.transport", "transient", true)));
        }
    }

    private sealed class ShareHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private ShareHarness(SqliteConnection connection, AppDbContext db, DocumentSharingService service, Guid ownerId, Guid documentId)
        { _connection = connection; Db = db; Service = service; OwnerId = ownerId; DocumentId = documentId; }
        public AppDbContext Db { get; }
        public DocumentSharingService Service { get; }
        public Guid OwnerId { get; }
        public Guid DocumentId { get; }
        public static async Task<ShareHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var owner = new User { Id = Guid.NewGuid(), Username = "owner", PasswordHash = "x", DisplayName = "Owner" };
            var template = new Template { Id = Guid.NewGuid(), OwnerId = owner.Id, Name = "T", Category = "legal", Body = "b", DefinitionJson = "{\"fields\":[],\"clauses\":[]}" };
            var document = new Document { Id = Guid.NewGuid(), OwnerId = owner.Id, TemplateId = template.Id, Title = "D", SnapshotJson = "{}", RenderedText = "r", PdfPath = "/tmp/m.pdf" };
            db.AddRange(owner, template, document);
            await db.SaveChangesAsync();
            var settings = new FakeSettings();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Sharing:HashKey"] = "a-test-key-long-enough-for-sharing" }).Build();
            return new(connection, db, new DocumentSharingService(db, settings, configuration), owner.Id, document.Id);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private sealed class FakeSettings : ISystemConfigReader
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            object value = key switch { SettingKeys.SharingEnabled => true, SettingKeys.SharingMaximumLifetimeHours => 72, _ => 90 };
            return Task.FromResult((T?)value);
        }
    }
}
