namespace OpenDockify.Generation.Services;

public enum DocumentArchiveFilter
{
    Active,
    Archived,
    All,
}

public enum DocumentLibrarySort
{
    Newest,
    Oldest,
    Title,
}

public sealed record DocumentLibraryQuery(
    string? Search,
    DocumentArchiveFilter Archive,
    DocumentLibrarySort Sort)
{
    public const int MaxSearchLength = 100;

    public static bool TryCreate(
        string? search,
        string? archive,
        string? sort,
        out DocumentLibraryQuery query,
        out string? error)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (normalizedSearch?.Length > MaxSearchLength)
        {
            query = default!;
            error = $"Search must be at most {MaxSearchLength} characters.";
            return false;
        }

        if (!TryParseArchive(archive, out var archiveFilter))
        {
            query = default!;
            error = "Archive must be active, archived, or all.";
            return false;
        }

        if (!TryParseSort(sort, out var sortOrder))
        {
            query = default!;
            error = "Sort must be newest, oldest, or title.";
            return false;
        }

        query = new DocumentLibraryQuery(normalizedSearch, archiveFilter, sortOrder);
        error = null;
        return true;
    }

    private static bool TryParseArchive(string? raw, out DocumentArchiveFilter value)
    {
        value = raw?.Trim().ToLowerInvariant() switch
        {
            null or "" or "active" => DocumentArchiveFilter.Active,
            "archived" => DocumentArchiveFilter.Archived,
            "all" => DocumentArchiveFilter.All,
            _ => (DocumentArchiveFilter)(-1),
        };
        return value >= DocumentArchiveFilter.Active && value <= DocumentArchiveFilter.All;
    }

    private static bool TryParseSort(string? raw, out DocumentLibrarySort value)
    {
        value = raw?.Trim().ToLowerInvariant() switch
        {
            null or "" or "newest" => DocumentLibrarySort.Newest,
            "oldest" => DocumentLibrarySort.Oldest,
            "title" => DocumentLibrarySort.Title,
            _ => (DocumentLibrarySort)(-1),
        };
        return value >= DocumentLibrarySort.Newest && value <= DocumentLibrarySort.Title;
    }
}

public sealed record DocumentTitleValidation(string? Title, string? Error);

public sealed record DocumentVersionLink(Guid Id, Guid? ParentId, DateTime CreatedAt);

public static class DocumentLibraryPolicy
{
    public const int MaxTitleLength = 200;

    public static DocumentTitleValidation ValidateTitle(string? rawTitle)
    {
        var title = rawTitle?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            return new DocumentTitleValidation(null, "Document title is required.");
        }

        if (title.Length > MaxTitleLength)
        {
            return new DocumentTitleValidation(null, $"Document title must be at most {MaxTitleLength} characters.");
        }

        return new DocumentTitleValidation(title, null);
    }

    public static string ResolveTitle(string? storedTitle, string templateName)
    {
        return string.IsNullOrWhiteSpace(storedTitle) ? templateName : storedTitle;
    }

    public static IReadOnlyList<DocumentVersionLink> FindConnectedVersions(
        IReadOnlyCollection<DocumentVersionLink> links,
        Guid startId)
    {
        if (!links.Any(link => link.Id == startId))
        {
            return [];
        }

        var connectedIds = new HashSet<Guid> { startId };
        var pending = new Queue<Guid>();
        pending.Enqueue(startId);

        while (pending.TryDequeue(out var currentId))
        {
            foreach (var link in links)
            {
                Guid? neighbor = null;
                if (link.Id == currentId)
                {
                    neighbor = link.ParentId;
                }
                else if (link.ParentId == currentId)
                {
                    neighbor = link.Id;
                }

                if (neighbor is Guid neighborId && connectedIds.Add(neighborId))
                {
                    pending.Enqueue(neighborId);
                }
            }
        }

        return links
            .Where(link => connectedIds.Contains(link.Id))
            .OrderBy(link => link.CreatedAt)
            .ThenBy(link => link.Id)
            .ToList();
    }
}
