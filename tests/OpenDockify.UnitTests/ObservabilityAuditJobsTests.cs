using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenDockify.Data;
using OpenDockify.Data.Audit;
using OpenDockify.Operations;
using Platform.Auditing.Contracts;
using Platform.Auditing.Contracts.DependencyInjection;
using Platform.Jobs;
using Xunit;
using ContractAuditEvent = Platform.Auditing.Contracts.AuditEvent;
using StoredAuditEvent = OpenDockify.Data.Models.AuditEvent;

namespace OpenDockify.UnitTests;

/// <summary>
/// Black-box coverage of the <c>platform-observability-audit-jobs</c>
/// change: 90-day audit retention purge (default + override), backup
/// recurring-job registration (schedule set vs. disabled), and the
/// redacted audit sink (no password or token ever reaches the table).
/// </summary>
public sealed class ObservabilityAuditJobsTests
{
    [Fact]
    public async Task Retention_purges_only_events_beyond_default_90_day_window()
    {
        await using var db = CreateAuditDb(out _);
        var now = DateTime.UtcNow;
        db.Set<StoredAuditEvent>().AddRange(
            NewAuditEvent(now.AddDays(-100)),
            NewAuditEvent(now.AddDays(-10)));
        await db.SaveChangesAsync();

        var service = NewRetentionService(db, retentionDays: 90);
        var removed = await service.PurgeAsync();

        Assert.Equal(1, removed);
        Assert.Equal(1, await db.Set<StoredAuditEvent>().CountAsync());
        Assert.All(await db.Set<StoredAuditEvent>().ToListAsync(), e => Assert.True(e.OccurredAt > now.AddDays(-90)));
    }

    [Fact]
    public async Task Retention_window_is_overridable_and_oldest_events_go_first()
    {
        await using var db = CreateAuditDb(out _);
        var now = DateTime.UtcNow;
        db.Set<StoredAuditEvent>().AddRange(
            NewAuditEvent(now.AddDays(-10)),
            NewAuditEvent(now.AddDays(-5)));
        await db.SaveChangesAsync();

        var service = NewRetentionService(db, retentionDays: 7);
        var removed = await service.PurgeAsync();

        Assert.Equal(1, removed);
        var remaining = await db.Set<StoredAuditEvent>().SingleAsync();
        Assert.True(remaining.OccurredAt > now.AddDays(-7));
    }

    [Fact]
    public void Backup_schedule_registers_none_when_disabled_and_cron_when_set()
    {
        var disabled = BuildRegistry(new Dictionary<string, string?>
        {
            ["Backup:ScheduleHours"] = "0",
        });
        Assert.DoesNotContain(disabled.Registered, d => d.Name == "openDockify.operations.scheduledBackup");
        Assert.Contains(disabled.Registered, d => d.Name == "openDockify.operations.digestBackfill");

        var enabled = BuildRegistry(new Dictionary<string, string?>
        {
            ["Backup:ScheduleHours"] = "6",
            ["Backup:SchedulePassphraseFile"] = "/tmp/opencode-test-passphrase",
        });
        var backup = Assert.Single(enabled.Registered, d => d.Name == "openDockify.operations.scheduledBackup");
        Assert.Equal("0 */6 * * *", backup.Cron);
    }

    [Fact]
    public async Task Audit_pipeline_redacts_secrets_before_persisting()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
            services.AddPlatformAuditing(o =>
            {
                o.FailurePolicy = AuditFailurePolicy.FailClosed;
                o.PublishMode = AuditPublishMode.Synchronous;
            });
            services.RemoveAll<IAuditSink>();
            services.AddSingleton<IAuditSink, EntityAuditSink>();
            await using var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            }

            var recorder = provider.GetRequiredService<IAuditRecorder>();
            var login = ContractAuditEvent.Create("auth.login", "auth", AuditOutcome.Success, DateTimeOffset.UtcNow)
                .WithMetadata(new Dictionary<string, string>
                {
                    ["username"] = "admin",
                    ["password"] = "super-secret-pw",
                    ["refreshToken"] = "opaque-token-value",
                });
            await recorder.RecordAsync(login);

            await using var verify = provider.CreateAsyncScope();
            var stored = await verify.ServiceProvider.GetRequiredService<AppDbContext>()
                .Set<StoredAuditEvent>().SingleAsync(a => a.Action == "auth.login");
            Assert.NotNull(stored.MetadataJson);
            Assert.Contains("admin", stored.MetadataJson, StringComparison.Ordinal);
            Assert.DoesNotContain("super-secret-pw", stored.MetadataJson, StringComparison.Ordinal);
            Assert.DoesNotContain("opaque-token-value", stored.MetadataJson, StringComparison.Ordinal);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static AppDbContext CreateAuditDb(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AuditRetentionService NewRetentionService(AppDbContext db, int retentionDays)
    {
        var options = Options.Create(new AuditRetentionOptions { RetentionDays = retentionDays });
        return new AuditRetentionService(db, options);
    }

    private static StoredAuditEvent NewAuditEvent(DateTime occurredAt)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Action = "http.request",
            Category = "http",
            Outcome = "Success",
            Severity = "Information",
            OccurredAt = occurredAt,
        };
    }

    private static IRecurringJobRegistry BuildRegistry(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IRecurringJobRegistry, TestRecurringJobRegistry>();
        using var provider = services.BuildServiceProvider();
        provider.RegisterOperationsRecurringJobs(configuration);
        return provider.GetRequiredService<IRecurringJobRegistry>();
    }

    private sealed class TestRecurringJobRegistry : IRecurringJobRegistry
    {
        private readonly List<RecurringJobDescriptor> _registered = new();

        public IReadOnlyList<RecurringJobDescriptor> Registered => _registered;

        public void Register(RecurringJobDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (_registered.Any(d => d.Name == descriptor.Name))
                return;
            _registered.Add(descriptor);
        }
    }
}
