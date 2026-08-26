using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Generation.Models;
using OpenDockify.Integrations.Models;
using OpenDockify.Templates.Models;

namespace OpenDockify.UnitTests;

/// <summary>Shared SQLite/JSON plumbing for the automation-integrations tests.</summary>
internal static class AutomationTestHarness
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Opens a named shared-cache in-memory database. Pass the same name to
    /// open additional connections (each context gets its own connection so
    /// transactions do not interleave).
    /// </summary>
    public static SqliteConnection OpenConnection(string databaseName)
    {
        var connection = new SqliteConnection($"Data Source=file:{databaseName}?mode=memory&cache=shared");
        connection.Open();
        return connection;
    }

    public static string NewDatabaseName()
    {
        return $"automation-{Guid.NewGuid():N}";
    }

    public static TestIntegrationsDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestIntegrationsDbContext>()
            .UseSqlite(connection)
            .Options;
        return new TestIntegrationsDbContext(options);
    }
}

internal sealed class TestIntegrationsDbContext(DbContextOptions<TestIntegrationsDbContext> options)
    : DbContext(options)
{
    public DbSet<ServiceToken> ServiceTokens => Set<ServiceToken>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<TemplateRevision> TemplateRevisions => Set<TemplateRevision>();

    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceToken).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Template).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Document).Assembly);
    }
}
