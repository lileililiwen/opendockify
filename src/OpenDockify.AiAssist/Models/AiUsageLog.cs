namespace OpenDockify.AiAssist.Models;

/// <summary>
/// A single AI invocation record. Prompt/response snippets are stored
/// redacted (ID numbers / name-like values masked) so raw PII never persists.
/// <see cref="Timestamp"/> is UTC <see cref="DateTime"/> (SQLite-compatible
/// for ordering).
/// </summary>
public sealed class AiUsageLog
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>e.g. <c>polish-clause</c>, <c>polish-document</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public string RequestSnippet { get; set; } = string.Empty;

    public string ResponseSnippet { get; set; } = string.Empty;

    public bool Success { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
