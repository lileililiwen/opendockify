namespace OpenDockify.Operations.Models;

public sealed record BackupManifest(
    int FormatVersion,
    string ApplicationVersion,
    string DatabaseProvider,
    DateTime CreatedAtUtc,
    IReadOnlyList<BackupManifestFile> Files);

public sealed record BackupManifestFile(string Path, long Size, string Sha256);

public sealed record BackupValidationResult(
    bool IsValid,
    string? BundleDigest,
    string? Receipt,
    string DatabaseProvider,
    IReadOnlyDictionary<string, int> ObjectCounts,
    int RequiredDowntimeMinutes,
    string? Error);

public sealed record IntegrityIssue(string Code, Guid? DocumentId, string Message);
public sealed record IntegrityReport(DateTime CheckedAtUtc, int DocumentsChecked, IReadOnlyList<IntegrityIssue> Issues)
{
    public bool IsHealthy => Issues.Count == 0;
}
