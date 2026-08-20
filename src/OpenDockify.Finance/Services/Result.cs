namespace OpenDockify.Finance.Services;

/// <summary>
/// Simple value-or-error result used by pure finance services, so invalid
/// input (e.g. a negative amount) surfaces as an error rather than silent
/// output.
/// </summary>
public sealed record Result<T>(bool Succeeded, T? Value, string? Error);

/// <summary>
/// Factory for <see cref="Result{T}"/>. Separate non-generic type so
/// construction stays analyzer-clean (CA1000).
/// </summary>
public static class Result
{
    public static Result<T> Success<T>(T value)
    {
        return new(true, value, null);
    }

    public static Result<T> Failure<T>(string error)
    {
        return new(false, default, error);
    }
}
