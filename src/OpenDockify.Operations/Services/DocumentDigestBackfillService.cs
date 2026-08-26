using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Generation.Models;

namespace OpenDockify.Operations.Services;

public sealed record DigestBackfillReport(int Scanned, int Updated, IReadOnlyList<Guid> MissingDocumentIds, bool HasMore);

public sealed class DocumentDigestBackfillService(DbContext db)
{
    public async Task<DigestBackfillReport> RunAsync(int maximumDocuments, CancellationToken ct = default)
    {
        var limit = Math.Clamp(maximumDocuments, 1, 500);
        var documents = await db.Set<Document>()
            .Where(x => x.ContentSha256 == null)
            .OrderBy(x => x.CreatedAt)
            .Take(limit + 1)
            .ToListAsync(ct);
        var hasMore = documents.Count > limit;
        var batch = documents.Take(limit).ToArray();
        var missing = new List<Guid>();
        var updated = 0;
        foreach (var document in batch)
        {
            if (!File.Exists(document.PdfPath))
            {
                missing.Add(document.Id);
                continue;
            }
            await using var input = File.OpenRead(document.PdfPath);
            document.ContentSha256 = Convert.ToHexString(await SHA256.HashDataAsync(input, ct)).ToLowerInvariant();
            updated++;
        }
        await db.SaveChangesAsync(ct);
        return new(batch.Length, updated, missing, hasMore);
    }
}
