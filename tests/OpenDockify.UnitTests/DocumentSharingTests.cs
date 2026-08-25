using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenDockify.Auth.Models;
using OpenDockify.Data;
using OpenDockify.Generation.Models;
using OpenDockify.Sharing.Models;
using OpenDockify.Sharing.Services;
using OpenDockify.SystemConfig.Services;
using OpenDockify.Templates.Models;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class DocumentSharingTests
{
    [Fact]
    public async Task Authorization_matrix_denies_by_default_and_revokes_immediately()
    {
        await using var harness = await SharingHarness.CreateAsync();
        var authorizer = new SharingDocumentReadAuthorizer(harness.Db);

        Assert.True((await authorizer.AuthorizeAsync(harness.OwnerId, harness.DocumentId, default)).IsOwner);
        Assert.False((await authorizer.AuthorizeAsync(harness.ViewerId, harness.DocumentId, default)).Allowed);
        await harness.Service.CreateGrantAsync(harness.OwnerId, harness.DocumentId, "viewer", "view", default);
        var access = await authorizer.AuthorizeAsync(harness.ViewerId, harness.DocumentId, default);
        Assert.True(access.Allowed);
        Assert.Equal("view", access.AccessLevel);
        Assert.False((await authorizer.AuthorizeAsync(Guid.NewGuid(), harness.DocumentId, default)).Allowed);

        var reviewer = await harness.Service.CreateGrantAsync(harness.OwnerId, harness.DocumentId, "viewer", "review", default);
        Assert.NotNull(reviewer.Grant);
        Assert.Equal("review", (await authorizer.AuthorizeAsync(harness.ViewerId, harness.DocumentId, default)).AccessLevel);
        await harness.Service.RevokeGrantAsync(harness.OwnerId, harness.DocumentId, reviewer.Grant.Id, default);
        Assert.False((await authorizer.AuthorizeAsync(harness.ViewerId, harness.DocumentId, default)).Allowed);
        harness.Db.Documents.Remove(await harness.Db.Documents.SingleAsync());
        await harness.Db.SaveChangesAsync();
        Assert.False((await authorizer.AuthorizeAsync(harness.OwnerId, harness.DocumentId, default)).Allowed);
    }

    [Fact]
    public async Task External_token_is_hash_only_expiring_policy_bounded_and_audit_redacted()
    {
        await using var harness = await SharingHarness.CreateAsync();
        var created = await harness.Service.CreateLinkAsync(harness.OwnerId, harness.DocumentId, 2, false, default);
        Assert.NotNull(created.Link);
        Assert.True(created.Link.Token.Length > 70);
        var stored = await harness.Db.ExternalShareLinks.SingleAsync();
        Assert.Equal(32, stored.SecretHash.Length);
        Assert.DoesNotContain(Convert.ToHexString(stored.SecretHash), created.Link.Token, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(await harness.Service.ResolvePublicAsync(created.Link.Token, false, "prefix:agent", default));
        Assert.Null(await harness.Service.ResolvePublicAsync(created.Link.Token, true, "prefix:agent", default));
        Assert.Null(await harness.Service.ResolvePublicAsync("malformed", false, "prefix:agent", default));
        stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await harness.Db.SaveChangesAsync();
        Assert.Null(await harness.Service.ResolvePublicAsync(created.Link.Token, false, "prefix:agent", default));

        var audit = await harness.Db.ShareAuditEvents.ToListAsync();
        Assert.Contains(audit, x => x.Action == "public-view" && x.Succeeded);
        Assert.Contains(audit, x => x.Action == "public-denied" && !x.Succeeded);
        Assert.All(audit, x =>
        {
            Assert.DoesNotContain(created.Link.Token, x.CoarseClient);
            Assert.DoesNotContain("rendered", x.CoarseClient, StringComparison.OrdinalIgnoreCase);
        });
    }

    private sealed class SharingHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private SharingHarness(SqliteConnection connection, AppDbContext db, DocumentSharingService service,
            Guid ownerId, Guid viewerId, Guid documentId)
        { _connection = connection; Db = db; Service = service; OwnerId = ownerId; ViewerId = viewerId; DocumentId = documentId; }
        public AppDbContext Db { get; }
        public DocumentSharingService Service { get; }
        public Guid OwnerId { get; }
        public Guid ViewerId { get; }
        public Guid DocumentId { get; }
        public static async Task<SharingHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var owner = new User { Id = Guid.NewGuid(), Username = "owner", PasswordHash = "x", DisplayName = "Owner" };
            var viewer = new User { Id = Guid.NewGuid(), Username = "viewer", PasswordHash = "x", DisplayName = "Viewer" };
            var template = new Template { Id = Guid.NewGuid(), OwnerId = owner.Id, Name = "Template", Category = "legal", Body = "body", DefinitionJson = "{\"fields\":[],\"clauses\":[]}" };
            var document = new Document { Id = Guid.NewGuid(), OwnerId = owner.Id, TemplateId = template.Id, Title = "Shared", SnapshotJson = "{}", RenderedText = "rendered", PdfPath = "/tmp/missing.pdf" };
            db.AddRange(owner, viewer, template, document);
            await db.SaveChangesAsync();
            var settings = new FakeSettings(true, 72, 90);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Sharing:HashKey"] = "a-test-key-long-enough-for-sharing" }).Build();
            return new(connection, db, new DocumentSharingService(db, settings, configuration), owner.Id, viewer.Id, document.Id);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private sealed class FakeSettings(bool enabled, int maximumHours, int retentionDays) : ISystemConfigReader
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            object value = key switch { SettingKeys.SharingEnabled => enabled, SettingKeys.SharingMaximumLifetimeHours => maximumHours, _ => retentionDays };
            return Task.FromResult((T?)value);
        }
    }
}
