using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Generation.Models;
using OpenDockify.Operations.Models;
using Platform.Storage.Contracts;
using Platform.Storage.Keys;

namespace OpenDockify.Operations.Services;

public sealed class ArchiveIntegrityService(DbContext db, IObjectStorage objectStorage)
{
    public async Task<IntegrityReport> CheckAsync(CancellationToken ct = default)
    {
        var issues = new List<IntegrityIssue>();
        var documents = await db.Set<Document>().AsNoTracking()
            .Select(x => new { x.Id, x.PdfPath, x.PdfStorageKey, x.ContentSha256 })
            .ToListAsync(ct);
        foreach (var document in documents)
        {
            var (found, stream) = await TryOpenAsync(document.PdfStorageKey, document.PdfPath, ct);
            if (!found)
            {
                issues.Add(new("missing-pdf", document.Id, "The persisted PDF is missing."));
                continue;
            }
            await using (stream)
            {
                var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(document.ContentSha256))
                    issues.Add(new("missing-digest", document.Id, "The PDF has no stored content digest."));
                else if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(digest), Convert.FromHexString(document.ContentSha256)))
                    issues.Add(new("digest-mismatch", document.Id, "The persisted PDF digest does not match."));
            }
        }

        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            issues.Add(new("pending-migrations", null, "Database migrations are pending."));
        return new(DateTime.UtcNow, documents.Count, issues);
    }

    private async Task<(bool Found, Stream Stream)> TryOpenAsync(string? storageKey, string legacyPath, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(storageKey))
        {
            var result = await objectStorage.DownloadAsync(new StorageObjectKey(storageKey), ct);
            if (result.Status == StorageOutcomeStatus.Succeeded && result.Value is not null)
            {
                // The stream is owned by the caller through the StorageReadResult;
                // we hand back the inner stream so the `await using` in the caller
                // can dispose it deterministically.
                return (true, result.Value.Content);
            }
        }

        if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath))
        {
            return (true, new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        return (false, Stream.Null);
    }
}
