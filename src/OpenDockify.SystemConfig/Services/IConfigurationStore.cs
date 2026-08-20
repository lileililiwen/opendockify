using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenDockify.SystemConfig.Models;

namespace OpenDockify.SystemConfig.Services;

/// <summary>
/// Resolves a setting's effective JSON value with precedence:
/// environment variable &gt; database value &gt; allowlist default.
/// </summary>
public interface IConfigurationStore
{
    /// <summary>
    /// Returns the effective value (as JSON text) and which layer it came
    /// from. Returns a resolution with <see cref="SettingResolution.Source"/>
    /// <c>"default"</c> and a null value when no default exists for the key.
    /// Throws <see cref="InvalidOperationException"/> when an environment
    /// override is set but invalid for the key's type.
    /// </summary>
    Task<SettingResolution> GetEffectiveValueAsync(string key, CancellationToken cancellationToken);
}

public sealed record SettingResolution(string? JsonValue, string Source)
{
    public static SettingResolution Environment(string jsonValue)
    {
        return new(jsonValue, "environment");
    }

    public static SettingResolution Database(string jsonValue)
    {
        return new(jsonValue, "database");
    }

    public static SettingResolution Default(string? jsonValue)
    {
        return new(jsonValue, "default");
    }
}

/// <summary>
/// Merges the three configuration layers. The env layer is read through
/// <see cref="IConfiguration"/> (already bound to environment variables), so
/// env parsing is not maintained twice.
/// </summary>
public sealed class ConfigurationStore(DbContext db, IConfiguration configuration) : IConfigurationStore
{
    public async Task<SettingResolution> GetEffectiveValueAsync(string key, CancellationToken cancellationToken)
    {
        var definition = SettingKeys.Find(key)
            ?? throw new InvalidOperationException($"Setting '{key}' is not in the allowlist.");

        var envValue = definition.EnvName is null ? null : configuration[definition.EnvName];
        if (!string.IsNullOrEmpty(envValue))
        {
            if (!SettingKeys.TryValidate(definition, envValue, out var jsonValue, out var error))
            {
                throw new InvalidOperationException(
                    $"Environment variable {definition.EnvName} is invalid for '{key}': {error}");
            }

            return SettingResolution.Environment(jsonValue);
        }

        var row = await db.Set<Setting>()
            .SingleOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (row is not null)
        {
            return SettingResolution.Database(row.ValueJson);
        }

        return SettingResolution.Default(definition.DefaultJson);
    }
}
