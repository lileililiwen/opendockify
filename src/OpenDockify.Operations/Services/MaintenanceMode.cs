namespace OpenDockify.Operations.Services;

public sealed class MaintenanceMode
{
    private int _enabled;
    public bool IsEnabled => Volatile.Read(ref _enabled) == 1;
    public void Enter()
    {
        Interlocked.Exchange(ref _enabled, 1);
    }

    public void Exit()
    {
        Interlocked.Exchange(ref _enabled, 0);
    }
}
