namespace OpenDockify.Operations.Configuration;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";
    public string Directory { get; set; } = "/app/data/backups";
    public string DocumentsPath { get; set; } = "/app/data/documents";
    public string? SchedulePassphraseFile { get; set; }
    public int RetainCount { get; set; } = 7;
    public int RetainDays { get; set; } = 30;
    public int ScheduleHours { get; set; }
    public int MaximumEntryBytes { get; set; } = 256 * 1024 * 1024;
    public long MaximumBundleBytes { get; set; } = 2L * 1024 * 1024 * 1024;
    public string? NativeDumpExecutable { get; set; }
    public string? NativeRestoreExecutable { get; set; }
}
