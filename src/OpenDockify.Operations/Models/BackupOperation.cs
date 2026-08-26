namespace OpenDockify.Operations.Models;

public enum BackupOperationKind { Backup, Validation, Restore, Integrity }
public enum BackupOperationState { Running, Succeeded, Failed, RolledBack }

public sealed class BackupOperation
{
    public Guid Id { get; set; }
    public BackupOperationKind Kind { get; set; }
    public BackupOperationState State { get; set; }
    public string? BundleDigest { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}
