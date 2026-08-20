namespace OpenDockify.SystemConfig.Models;

/// <summary>
/// A single global setting. Values are stored as JSON text so every setting
/// key can hold any serializable value; typed access happens in
/// <see cref="Services.SystemConfigService"/>.
/// </summary>
public sealed class Setting
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string ValueJson { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
