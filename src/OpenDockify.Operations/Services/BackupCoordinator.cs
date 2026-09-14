using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenDockify.Generation.Models;
using OpenDockify.Operations.Configuration;
using OpenDockify.Operations.Models;
using Platform.Storage.Contracts;
using Platform.Storage.Keys;

namespace OpenDockify.Operations.Services;

public sealed class BackupCoordinator(
    DbContext db,
    DatabaseSnapshotService snapshots,
    BackupBundleService bundles,
    RestoreReceiptService receipts,
    MaintenanceMode maintenance,
    ArchiveIntegrityService integrity,
    BackupRetentionService retention,
    IConfiguration configuration,
    IOptions<BackupOptions> options,
    IObjectStorage objectStorage)
{
    private readonly BackupOptions _options = options.Value;

    public async Task<(Guid OperationId, string Path, string Digest)> CreateAsync(string passphrase, CancellationToken ct)
    {
        var operation = Start(BackupOperationKind.Backup);
        var staging = Temp("backup");
        var bundleName = $"opendockify-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{operation.Id:N}.odbak";
        var storageKey = new StorageObjectKey($"backups/{bundleName}");
        Directory.CreateDirectory(staging);
        try
        {
            await snapshots.CreateSnapshotAsync(Path.Combine(staging, "database"), ct);
            await CopyDocumentsAsync(Path.Combine(staging, "documents"), ct);
            Directory.CreateDirectory(_options.Directory);
            var tempBundle = Path.Combine(Path.GetTempPath(), $"opendockify-bundle-{operation.Id:N}.odbak");
            var digest = await bundles.CreateAsync(staging, tempBundle, passphrase, snapshots.ProviderName, ct);
            await UploadBundleAsync(tempBundle, storageKey, ct);
            if (File.Exists(tempBundle))
            {
                File.Delete(tempBundle);
            }
            await CompleteAsync(operation, BackupOperationState.Succeeded, digest, "Backup created.", ct, storageKey.Value);
            retention.Apply(DateTime.UtcNow);
            return (operation.Id, storageKey.Value, digest);
        }
        catch (Exception ex)
        {
            await CompleteAsync(operation, BackupOperationState.Failed, null, Safe(ex), CancellationToken.None);
            throw;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    public async Task<BackupValidationResult> ValidateAsync(string path, string passphrase, CancellationToken ct)
    {
        var operation = Start(BackupOperationKind.Validation);
        string? extracted = null;
        var storageKey = ResolveStorageKey(path);
        string? downloadedBundle = null;
        try
        {
            downloadedBundle = await DownloadBundleAsync(storageKey, ct);
            var result = await bundles.ValidateAndExtractAsync(downloadedBundle, passphrase, ct);
            extracted = result.ExtractedDirectory;
            if (!string.Equals(result.Manifest.DatabaseProvider, snapshots.ProviderName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Cross-provider restore is not supported.");
            var counts = await ReadCountsAsync(extracted, result.Manifest.DatabaseProvider, ct);
            var receipt = receipts.Issue(result.Digest);
            await CompleteAsync(operation, BackupOperationState.Succeeded, result.Digest, "Backup validated.", ct, storageKey.Value);
            return new(true, result.Digest, receipt, result.Manifest.DatabaseProvider, counts, 5, null);
        }
        catch (Exception ex)
        {
            await CompleteAsync(operation, BackupOperationState.Failed, null, Safe(ex), CancellationToken.None);
            return new(false, null, null, snapshots.ProviderName, new Dictionary<string, int>(), 0, Safe(ex));
        }
        finally
        {
            if (extracted is not null && Directory.Exists(extracted))
                Directory.Delete(extracted, true);
            if (downloadedBundle is not null && File.Exists(downloadedBundle))
                File.Delete(downloadedBundle);
        }
    }

    public async Task<Guid> RestoreAsync(string path, string passphrase, string digest, string receipt, CancellationToken ct)
    {
        if (!receipts.Consume(receipt, digest))
            throw new InvalidOperationException("Validation receipt is invalid or expired.");
        var operation = Start(BackupOperationKind.Restore);
        string? extracted = null;
        string? rollback = null;
        string? downloadedBundle = null;
        maintenance.Enter();
        try
        {
            var storageKey = ResolveStorageKey(path);
            downloadedBundle = await DownloadBundleAsync(storageKey, ct);
            var validated = await bundles.ValidateAndExtractAsync(downloadedBundle, passphrase, ct);
            extracted = validated.ExtractedDirectory;
            if (!string.Equals(validated.Digest, digest, StringComparison.Ordinal))
                throw new InvalidOperationException("Bundle changed after validation.");
            if (!string.Equals(validated.Manifest.DatabaseProvider, snapshots.ProviderName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Cross-provider restore is not supported.");
            if (snapshots.ProviderName != "sqlite")
                throw new InvalidOperationException("External-provider restore requires the documented offline native restore command.");

            rollback = Temp("rollback");
            Directory.CreateDirectory(rollback);
            await RestoreSqliteAsync(extracted, rollback);
            await RestoreDocumentsAsync(extracted, rollback, ct);
            await VerifyRestoredSqliteAsync(ct);
            await CompleteAsync(operation, BackupOperationState.Succeeded, digest, "Restore verified.", ct, storageKey.Value);
            Directory.Delete(rollback, true);
            rollback = null;
            maintenance.Exit();
            return operation.Id;
        }
        catch (Exception ex)
        {
            if (rollback is not null)
            {
                await RollbackAsync(rollback, CancellationToken.None);
                await CompleteAsync(operation, BackupOperationState.RolledBack, digest, "Restore failed; rollback applied.", CancellationToken.None);
            }
            else
                await CompleteAsync(operation, BackupOperationState.Failed, digest, Safe(ex), CancellationToken.None);
            throw;
        }
        finally
        {
            if (extracted is not null && Directory.Exists(extracted))
                Directory.Delete(extracted, true);
            if (downloadedBundle is not null && File.Exists(downloadedBundle))
                File.Delete(downloadedBundle);
        }
    }

    public Task<IntegrityReport> CheckIntegrityAsync(CancellationToken ct)
    {
        return integrity.CheckAsync(ct);
    }

    private async Task UploadBundleAsync(string sourcePath, StorageObjectKey key, CancellationToken ct)
    {
        var info = new FileInfo(sourcePath);
        await using var input = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var request = new StorageUploadRequest(key, input, "application/octet-stream", info.Length);
        var outcome = await objectStorage.UploadAsync(request, ct);
        if (outcome.Status != StorageOutcomeStatus.Succeeded)
        {
            var message = outcome.Failure is null ? "backup upload failed" : outcome.Failure.Message;
            throw new InvalidOperationException($"Backup upload to object storage failed: {message}");
        }
    }

    private async Task<string> DownloadBundleAsync(StorageObjectKey key, CancellationToken ct)
    {
        var result = await objectStorage.DownloadAsync(key, ct);
        if (result.Status != StorageOutcomeStatus.Succeeded || result.Value is null)
        {
            var message = result switch
            {
                { Status: StorageOutcomeStatus.NotFound } => "Backup bundle was not found in object storage.",
                { Failure: { } f } => f.Message,
                _ => "Backup bundle could not be downloaded from object storage.",
            };
            throw new InvalidOperationException(message);
        }

        var temp = Path.Combine(Path.GetTempPath(), $"opendockify-bundle-dl-{Guid.NewGuid():N}.odbak");
        await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await result.Value.Content.CopyToAsync(output, ct);
        }
        await result.Value.DisposeAsync();
        return temp;
    }

    private static StorageObjectKey ResolveStorageKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Backup storage key is required.");
        // Accept either a full forward-slash storage key (`backups/foo.odbak`) or a
        // bare bundle name (`opendockify-...odbak`); the legacy absolute file path
        // is no longer supported because bundles now live in object storage.
        var trimmed = path.Trim();
        if (trimmed.Contains('\\') || Path.IsPathRooted(trimmed))
        {
            throw new InvalidOperationException("Backup bundles are now identified by their object storage key (e.g. 'backups/name.odbak').");
        }
        return trimmed.StartsWith("backups/", StringComparison.OrdinalIgnoreCase)
            ? new StorageObjectKey(trimmed)
            : new StorageObjectKey($"backups/{trimmed}");
    }

    private BackupOperation Start(BackupOperationKind kind)
    {
        var operation = new BackupOperation { Id = Guid.NewGuid(), Kind = kind, State = BackupOperationState.Running };
        db.Set<BackupOperation>().Add(operation);
        return operation;
    }

    private async Task CompleteAsync(BackupOperation operation, BackupOperationState state, string? digest, string summary, CancellationToken ct, string? storageKey = null)
    {
        operation.State = state;
        operation.BundleDigest = digest;
        operation.BundleStorageKey = storageKey;
        operation.Summary = summary;
        operation.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task CopyDocumentsAsync(string destination, CancellationToken ct)
    {
        Directory.CreateDirectory(destination);
        var documents = await db.Set<Document>().AsNoTracking()
            .Select(x => new { x.Id, x.PdfPath, x.PdfStorageKey })
            .ToListAsync(ct);
        foreach (var document in documents)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, $"{document.Id:N}.pdf");
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var copied = false;
            if (!string.IsNullOrEmpty(document.PdfStorageKey))
            {
                var result = await objectStorage.DownloadAsync(new StorageObjectKey(document.PdfStorageKey), ct);
                if (result.Status == StorageOutcomeStatus.Succeeded && result.Value is not null)
                {
                    await result.Value.Content.CopyToAsync(output, ct);
                    await result.Value.DisposeAsync();
                    copied = true;
                }
            }
            if (!copied && !string.IsNullOrEmpty(document.PdfPath) && File.Exists(document.PdfPath))
            {
                await using var source = new FileStream(document.PdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await source.CopyToAsync(output, ct);
                copied = true;
            }
            if (!copied)
            {
                throw new InvalidDataException($"Document {document.Id} has no PDF.");
            }
        }
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadCountsAsync(string extracted, string provider, CancellationToken ct)
    {
        if (provider != "sqlite")
            return new Dictionary<string, int>();
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(extracted, "database", "database.sqlite")};Mode=ReadOnly");
        await connection.OpenAsync(ct);
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in new Dictionary<string, string>
        {
            ["Users"] = "SELECT COUNT(*) FROM \"Users\"",
            ["Templates"] = "SELECT COUNT(*) FROM \"Templates\"",
            ["Documents"] = "SELECT COUNT(*) FROM \"Documents\"",
        })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = query.Value;
            result[query.Key] = Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        }
        return result;
    }

    private async Task RestoreSqliteAsync(string extracted, string rollback)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=opendockify.db";
        var live = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
        var staged = Path.Combine(extracted, "database", "database.sqlite");
        var rollbackDb = Path.Combine(rollback, "database.sqlite");
        var source = (SqliteConnection)db.Database.GetDbConnection();
        await source.OpenAsync();
        await using (var rollbackConnection = new SqliteConnection($"Data Source={rollbackDb}"))
        {
            await rollbackConnection.OpenAsync();
            source.BackupDatabase(rollbackConnection);
        }
        await source.CloseAsync();
        SqliteConnection.ClearAllPools();
        DeleteSqliteSidecars(live);
        var replacement = live + ".restore-staged";
        File.Copy(staged, replacement, true);
        File.Move(replacement, live, true);
    }

    private async Task RestoreDocumentsAsync(string extracted, string rollback, CancellationToken ct)
    {
        var staged = Path.Combine(extracted, "documents");
        var live = Path.GetFullPath(_options.DocumentsPath);
        var rollbackDocs = Path.Combine(rollback, "documents");
        if (Directory.Exists(live))
            Directory.Move(live, rollbackDocs);
        Directory.CreateDirectory(live);
        if (!Directory.Exists(staged))
            return;
        foreach (var file in Directory.EnumerateFiles(staged, "*.pdf"))
        {
            ct.ThrowIfCancellationRequested();
            var id = Guid.Parse(Path.GetFileNameWithoutExtension(file));
            var document = await db.Set<Document>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
            var target = Path.GetFullPath(document.PdfPath);
            var root = live + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, StringComparison.Ordinal))
                target = Path.Combine(live, id.ToString("N") + ".pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);

            // Mirror the restored file into object storage so the new download
            // path (storage -> 206) works without a separate backfill job.
            var storageKey = !string.IsNullOrEmpty(document.PdfStorageKey)
                ? new StorageObjectKey(document.PdfStorageKey)
                : new StorageObjectKey($"pdfs/{document.OwnerId:N}/{id:N}.pdf");
            var info = new FileInfo(target);
            await using var input = new FileStream(
                target,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var outcome = await objectStorage.UploadAsync(
                new StorageUploadRequest(storageKey, input, "application/pdf", info.Length),
                ct);
            if (outcome.Status != StorageOutcomeStatus.Succeeded)
            {
                throw new InvalidOperationException(
                    outcome.Failure is null
                        ? "Restored PDF upload to object storage failed."
                        : $"Restored PDF upload to object storage failed: {outcome.Failure.Message}");
            }
        }
    }

    private async Task VerifyRestoredSqliteAsync(CancellationToken ct)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=opendockify.db";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        var result = Convert.ToString(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Restored SQLite database failed integrity_check.");
    }

    private async Task RollbackAsync(string rollback, CancellationToken ct)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=opendockify.db";
        var liveDb = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
        var rollbackDb = Path.Combine(rollback, "database.sqlite");
        await db.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();
        DeleteSqliteSidecars(liveDb);
        if (File.Exists(rollbackDb))
            File.Copy(rollbackDb, liveDb, true);
        var liveDocs = Path.GetFullPath(_options.DocumentsPath);
        if (Directory.Exists(liveDocs))
            Directory.Delete(liveDocs, true);
        var rollbackDocs = Path.Combine(rollback, "documents");
        if (Directory.Exists(rollbackDocs))
            Directory.Move(rollbackDocs, liveDocs);
        await VerifyRestoredSqliteAsync(ct);
        maintenance.Exit();
    }

    private static string Temp(string purpose)
    {
        return Path.Combine(Path.GetTempPath(), $"opendockify-{purpose}-{Guid.NewGuid():N}");
    }

    private static void DeleteSqliteSidecars(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var path = databasePath + suffix;
            if (File.Exists(path))
                File.Delete(path);
        }
    }
    private static string Safe(Exception ex)
    {
        return ex is InvalidDataException or InvalidOperationException or ArgumentException
        ? ex.Message : "Operation failed. Consult secret-free server logs.";
    }
}
