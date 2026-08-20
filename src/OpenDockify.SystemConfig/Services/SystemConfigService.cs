using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenDockify.SystemConfig.Models;

namespace OpenDockify.SystemConfig.Services;

/// <summary>
/// In-memory effective-value cache, keyed by setting key. Invalidated on
/// writes; per-instance (single-instance MVP, documented limitation).
/// </summary>
public sealed class SettingCache
{
    private readonly ConcurrentDictionary<string, SettingResolution> _entries = new();

    public bool TryGet(string key, out SettingResolution resolution)
    {
        return _entries.TryGetValue(key, out resolution!);
    }

    public void Set(string key, SettingResolution resolution)
    {
        _entries[key] = resolution;
    }

    public void Invalidate(string key)
    {
        _entries.TryRemove(key, out _);
    }
}

public sealed record SetSettingResult(bool Succeeded, string? Error)
{
    public static SetSettingResult Success()
    {
        return new(true, null);
    }

    public static SetSettingResult Failure(string error)
    {
        return new(false, error);
    }
}

/// <summary>Display view of a setting for the admin API.</summary>
public sealed record SettingView(string Key, string? Value, string Source, bool IsSecret, bool Masked);

/// <summary>
/// Typed read/write access to global settings. Modules depend on this service,
/// never on the <see cref="Setting"/> entity directly, so storage and override
/// precedence stay behind one interface.
/// </summary>
public sealed class SystemConfigService(DbContext db, IConfigurationStore store, SettingCache cache)
    : ISystemConfigReader
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads a setting's effective value as <typeparamref name="T"/>.
    /// Returns <c>default</c> when no value exists for the key.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var resolution = await GetResolutionAsync(key, cancellationToken);
        if (resolution.JsonValue is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(resolution.JsonValue, _jsonOptions);
    }

    /// <summary>
    /// Updates a setting. Rejects unknown keys and invalid values; persists the
    /// JSON value and invalidates the cache. Returns an error message on
    /// failure, otherwise a success result.
    /// </summary>
    public async Task<SetSettingResult> SetAsync(
        string key,
        string rawValue,
        CancellationToken cancellationToken = default)
    {
        var definition = SettingKeys.Find(key);
        if (definition is null)
        {
            return SetSettingResult.Failure($"Setting '{key}' is not in the allowlist.");
        }

        if (!SettingKeys.TryValidate(definition, rawValue, out var jsonValue, out var error))
        {
            return SetSettingResult.Failure(error ?? "Invalid value.");
        }

        var setting = await db.Set<Setting>()
            .SingleOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting is null)
        {
            setting = new Setting { Id = Guid.NewGuid(), Key = key };
            db.Set<Setting>().Add(setting);
        }

        setting.ValueJson = jsonValue;
        setting.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        cache.Invalidate(key);

        return SetSettingResult.Success();
    }

    /// <summary>
    /// Effective value for every allowlisted key, secrets masked. Used by the
    /// admin API.
    /// </summary>
    public async Task<IReadOnlyList<SettingView>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var views = new List<SettingView>(SettingKeys.All.Count);
        foreach (var definition in SettingKeys.All)
        {
            var resolution = await GetResolutionAsync(definition.Key, cancellationToken);
            var display = ToDisplayString(resolution.JsonValue);
            var isSecret = definition.IsSecret;

            views.Add(new SettingView(
                definition.Key,
                isSecret ? SecretRedactor.MaskSecret(display) : display,
                resolution.Source,
                isSecret,
                isSecret));
        }

        return views;
    }

    private async Task<SettingResolution> GetResolutionAsync(string key, CancellationToken cancellationToken)
    {
        if (cache.TryGet(key, out var cached))
        {
            return cached;
        }

        var resolution = await store.GetEffectiveValueAsync(key, cancellationToken);
        cache.Set(key, resolution);
        return resolution;
    }

    private static string? ToDisplayString(string? jsonValue)
    {
        if (string.IsNullOrEmpty(jsonValue))
        {
            return null;
        }

        var element = JsonSerializer.Deserialize<JsonElement>(jsonValue);
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }
}
