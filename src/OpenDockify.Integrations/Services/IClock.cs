namespace OpenDockify.Integrations.Services;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    private SystemClock() { }

    public DateTime UtcNow => DateTime.UtcNow;
}
