namespace OpenDockify.Integrations.Services;

/// <summary>
/// The closed set of least-privilege scopes a service token may carry. Every
/// automation endpoint declares exactly one required scope.
/// </summary>
public static class AutomationScopes
{
    public const string TemplatesRead = "templates:read";
    public const string DocumentsRead = "documents:read";
    public const string DocumentsPreview = "documents:preview";
    public const string DocumentsWrite = "documents:write";
    public const string OperationsRead = "operations:read";

    public static readonly IReadOnlyList<string> All =
    [
        TemplatesRead,
        DocumentsRead,
        DocumentsPreview,
        DocumentsWrite,
        OperationsRead,
    ];

    public static bool IsValid(string scope)
    {
        return All.Contains(scope);
    }

    public static IReadOnlyList<string> Parse(string commaSeparated)
    {
        return [.. commaSeparated
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)];
    }
}

/// <summary>Event types that can be subscribed to and delivered via webhooks.</summary>
public static class AutomationEvents
{
    public const string DocumentFinalizedName = "document.finalized";

    public static readonly IReadOnlyList<string> All = [DocumentFinalizedName];

    public const string DocumentFinalized = DocumentFinalizedName;

    public static bool IsValid(string eventType)
    {
        return All.Contains(eventType);
    }
}
