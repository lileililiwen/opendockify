using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Generation.Models;
using OpenDockify.Operations.Models;

namespace OpenDockify.Operations.Services;

public sealed class ArchiveIntegrityService(DbContext db)
{
    public async Task<IntegrityReport> CheckAsync(CancellationToken ct = default)
    {
        var issues = new List<IntegrityIssue>();
        var documents = await db.Set<Document>().AsNoTracking().ToListAsync(ct);
        foreach (var document in documents)
        {
            if (!File.Exists(document.PdfPath))
            {
                issues.Add(new("missing-pdf", document.Id, "The persisted PDF is missing."));
                continue;
            }
            await using var input = File.OpenRead(document.PdfPath);
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(input, ct)).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(document.ContentSha256))
                issues.Add(new("missing-digest", document.Id, "The PDF has no stored content digest."));
            else if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(digest), Convert.FromHexString(document.ContentSha256)))
                issues.Add(new("digest-mismatch", document.Id, "The persisted PDF digest does not match."));
        }

        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            issues.Add(new("pending-migrations", null, "Database migrations are pending."));
        return new(DateTime.UtcNow, documents.Count, issues);
    }
}
