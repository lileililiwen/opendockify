namespace OpenDockify.Data.Audit;

/// <summary>
/// Configuration for the <see cref="AuditRetentionService"/>. Bound from
/// the <c>Audit</c> configuration section. <see cref="RetentionDays"/>
/// defaults to 90 days and may be overridden by environment.
/// </summary>
public sealed class AuditRetentionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Audit";

    /// <summary>Default retention window, in days.</summary>
    public const int DefaultRetentionDays = 90;

    /// <summary>The number of days audit events are retained.</summary>
    public int RetentionDays { get; set; } = DefaultRetentionDays;
}
