namespace OpenDockify.Auth.Services;

/// <summary>
/// Result of a user-scoped resource lookup. Owned-resource endpoints return
/// <see cref="NotFound"/> for ids that are foreign OR absent, so the API never
/// leaks whether a resource exists (multi-user isolation, spec negative case).
/// Use <see cref="OwnedResourceResult"/> to construct instances.
/// </summary>
public sealed record OwnedResourceResult<T>(T? Value, bool NotFound);

/// <summary>
/// Factory for <see cref="OwnedResourceResult{T}"/>. Separate non-generic type
/// so construction stays analyzer-clean (CA1000).
/// </summary>
public static class OwnedResourceResult
{
    public static OwnedResourceResult<T> Found<T>(T value)
    {
        return new(value, false);
    }

    public static OwnedResourceResult<T> Missing<T>()
    {
        return new(default, true);
    }
}
