using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.SystemConfig.Models;
using OpenDockify.SystemConfig.Services;

namespace OpenDockify.Data;

/// <summary>
/// Seeds default settings when the Settings table is empty: <c>Ai.Enabled</c>
/// defaults to <c>false</c> (AI assist off until a deployer enables it) and
/// <c>Lpr.OneYearRate</c> defaults to a configurable value
/// (<c>Seed:LprOneYearRate</c>, env <c>Seed__LprOneYearRate</c>). Idempotent:
/// a key is only inserted when it has no row yet, so restarts never duplicate
/// defaults.
///
/// Lives in OpenDockify.Data (the central reference) because it combines the
/// SystemConfig module's entities/allowlist with the seed registry — domain
/// modules must not reference OpenDockify.Data, so composite seeders live here.
/// </summary>
public sealed class SettingsSeeder : IDbSeeder
{
    public int Order => 110;

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var config = services.GetRequiredService<IConfiguration>();

        var defaults = new Dictionary<string, string>
        {
            [SettingKeys.AiEnabled] = "false",
            [SettingKeys.LprOneYearRate] = LprOneYearRateDefault(config),
        };

        foreach (var (key, jsonValue) in defaults)
        {
            var exists = await db.Set<Setting>().AnyAsync(s => s.Key == key, cancellationToken);
            if (exists)
            {
                continue;
            }

            db.Set<Setting>().Add(new Setting
            {
                Id = Guid.NewGuid(),
                Key = key,
                ValueJson = jsonValue,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string LprOneYearRateDefault(IConfiguration config)
    {
        var configured = config["Seed:LprOneYearRate"];
        if (decimal.TryParse(configured, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return SettingKeys.DefaultLprOneYearRate.ToString(CultureInfo.InvariantCulture);
    }
}
