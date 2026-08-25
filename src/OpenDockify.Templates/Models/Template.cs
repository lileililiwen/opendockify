namespace OpenDockify.Templates.Models;

/// <summary>
/// A contract template. Ownership is two-tier: <see cref="OwnerId"/> is null
/// for system built-ins and admin global templates, set for private templates.
/// Built-ins are read-only for everyone; admin global templates are editable
/// by any Administrator.
/// </summary>
public sealed class Template
{
    public Guid Id { get; set; }

    public Guid StableId { get; set; } = Guid.NewGuid();

    public int CurrentRevision { get; set; } = 1;

    public string? SourceInstance { get; set; }

    public Guid? SourceStableId { get; set; }

    public Guid? OwnerId { get; set; }

    public bool IsBuiltIn { get; set; }

    public bool IsPublic { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string RiskNoticeText { get; set; } = string.Empty;

    /// <summary>Template body text with <c>{{field}}</c> placeholders.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>JSON definition (fields + clauses), validated on write.</summary>
    public string DefinitionJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
