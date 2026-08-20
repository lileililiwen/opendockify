namespace OpenDockify.SystemConfig.Services;

/// <summary>
/// Typed read access to global settings, owned by system-config. Domain
/// modules (e.g. OpenDockify.Finance) depend on this abstraction instead of the
/// concrete service so the storage/override mechanics stay encapsulated.
/// </summary>
public interface ISystemConfigReader
{
    /// <summary>
    /// Reads a setting's effective value as <typeparamref name="T"/>.
    /// Returns <c>default</c> when no value exists for the key.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
}
