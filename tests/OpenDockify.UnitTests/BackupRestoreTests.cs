using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenDockify.Generation.Models;
using OpenDockify.Operations.Configuration;
using OpenDockify.Operations.Services;
using Platform.Storage.Local;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class BackupRestoreTests
{
    private const string _passphrase = "correct horse battery staple";

    [Fact]
    public async Task Bundle_round_trip_rejects_wrong_key_corruption_and_truncation()
    {
        var root = Temp();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        await File.WriteAllTextAsync(Path.Combine(source, "nested", "record.txt"), "durable record");
        var options = Options.Create(new BackupOptions { MaximumBundleBytes = 4 * 1024 * 1024 });
        var service = new BackupBundleService(options);
        var bundle = Path.Combine(root, "valid.odbak");
        try
        {
            var digest = await service.CreateAsync(source, bundle, _passphrase, "sqlite");
            Assert.True(BackupBundleService.HasValidHeader(bundle));
            var restored = await service.ValidateAndExtractAsync(bundle, _passphrase);
            Assert.Equal(digest, restored.Digest);
            Assert.Equal("durable record", await File.ReadAllTextAsync(Path.Combine(restored.ExtractedDirectory, "nested", "record.txt")));
            Directory.Delete(restored.ExtractedDirectory, true);

            await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAndExtractAsync(bundle, "incorrect password value"));

            var corrupt = Path.Combine(root, "corrupt.odbak");
            File.Copy(bundle, corrupt);
            var bytes = await File.ReadAllBytesAsync(corrupt);
            bytes[^20] ^= 0x40;
            await File.WriteAllBytesAsync(corrupt, bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAndExtractAsync(corrupt, _passphrase));

            var truncated = Path.Combine(root, "truncated.odbak");
            await File.WriteAllBytesAsync(truncated, bytes[..(bytes.Length / 2)]);
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAndExtractAsync(truncated, _passphrase));

            var unsupported = Path.Combine(root, "unsupported.odbak");
            var versionBytes = await File.ReadAllBytesAsync(bundle);
            versionBytes[8] = 2;
            await File.WriteAllBytesAsync(unsupported, versionBytes);
            Assert.False(BackupBundleService.HasValidHeader(unsupported));
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAndExtractAsync(unsupported, _passphrase));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Bundle_rejects_oversized_entries_and_does_not_publish_output()
    {
        var root = Temp();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        await File.WriteAllBytesAsync(Path.Combine(source, "large.bin"), new byte[128]);
        var output = Path.Combine(root, "rejected.odbak");
        var service = new BackupBundleService(Options.Create(new BackupOptions { MaximumEntryBytes = 64 }));
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => service.CreateAsync(source, output, _passphrase, "sqlite"));
            Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Bundle_creation_rejects_normalized_path_traversal_names()
    {
        if (OperatingSystem.IsWindows())
            return;
        var root = Temp();
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "..\\escape.txt"), "unsafe");
        var output = Path.Combine(Temp(), "rejected.odbak");
        var service = new BackupBundleService(Options.Create(new BackupOptions()));
        try
        { await Assert.ThrowsAsync<InvalidDataException>(() => service.CreateAsync(root, output, _passphrase, "sqlite")); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Integrity_check_reports_missing_and_altered_documents_without_repair()
    {
        var root = Temp();
        Directory.CreateDirectory(root);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestOperationsDbContext>().UseSqlite(connection).Options;
        await using var db = new TestOperationsDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var alteredPath = Path.Combine(root, "altered.pdf");
        await File.WriteAllTextAsync(alteredPath, "altered");
        var missing = Guid.NewGuid();
        var altered = Guid.NewGuid();
        db.Documents.AddRange(
            Document(missing, Path.Combine(root, "missing.pdf"), new string('0', 64)),
            Document(altered, alteredPath, new string('1', 64)));
        await db.SaveChangesAsync();
        try
        {
            var objectsRoot = Path.Combine(root, "objects");
            Directory.CreateDirectory(objectsRoot);
            var report = await new ArchiveIntegrityService(db, new LocalFileStorage(objectsRoot)).CheckAsync();
            Assert.Contains(report.Issues, x => x.Code == "missing-pdf" && x.DocumentId == missing);
            Assert.Contains(report.Issues, x => x.Code == "digest-mismatch" && x.DocumentId == altered);
            Assert.Equal("altered", await File.ReadAllTextAsync(alteredPath));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Retention_preserves_unknown_newest_and_symlink_files()
    {
        var root = Temp();
        var external = Temp();
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(external);
        try
        {
            File.WriteAllText(Path.Combine(root, "unknown.txt"), "keep");
            File.WriteAllText(Path.Combine(external, "outside.odbak"), "outside");
            File.CreateSymbolicLink(Path.Combine(root, "linked.odbak"), Path.Combine(external, "outside.odbak"));
            var service = new BackupRetentionService(Options.Create(new BackupOptions { Directory = root, RetainCount = 1, RetainDays = 1 }));
            Assert.Empty(service.Apply(DateTime.UtcNow));
            Assert.True(File.Exists(Path.Combine(root, "unknown.txt")));
            Assert.True(File.Exists(Path.Combine(external, "outside.odbak")));
        }
        finally { Directory.Delete(root, true); Directory.Delete(external, true); }
    }

    [Fact]
    public void Validation_receipt_is_digest_bound_expiring_and_single_use()
    {
        var service = new RestoreReceiptService();
        var receipt = service.Issue(new string('a', 64));
        Assert.False(service.Consume(receipt, new string('b', 64)));
        receipt = service.Issue(new string('a', 64));
        Assert.True(service.Consume(receipt, new string('a', 64)));
        Assert.False(service.Consume(receipt, new string('a', 64)));
    }

    [Fact]
    public async Task External_provider_snapshot_fails_closed_without_native_adapter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<TestOperationsDbContext>().UseSqlite(connection).Options;
        await using var db = new TestOperationsDbContext(options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "postgres",
        }).Build();
        var service = new DatabaseSnapshotService(db, config, Options.Create(new BackupOptions()));
        var destination = Temp();
        try
        { await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSnapshotAsync(destination, default)); }
        finally { Directory.Delete(destination, true); }
    }

    private static Document Document(Guid id, string path, string digest)
    {
        return new()
        {
            Id = id,
            OwnerId = Guid.NewGuid(),
            TemplateId = Guid.NewGuid(),
            Title = "test",
            SnapshotJson = "{}",
            RenderedText = "text",
            PdfPath = path,
            ContentSha256 = digest,
        };
    }

    private static string Temp()
    {
        return Path.Combine(Path.GetTempPath(), $"opendockify-test-{Guid.NewGuid():N}");
    }

    private sealed class TestOperationsDbContext(DbContextOptions<TestOperationsDbContext> options) : DbContext(options)
    {
        public DbSet<Document> Documents => Set<Document>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(Document).Assembly);
        }
    }
}
