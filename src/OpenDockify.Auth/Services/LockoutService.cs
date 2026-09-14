using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenDockify.Auth.Models;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Tracks login attempts and gates the <c>login</c> endpoint with a
/// user+IP sliding-window lockout. Defaults: 5 failures within 15 minutes
/// triggers a 429; the same user+IP pair resets the counter on first
/// success. Configuration overrides live in <c>Auth:Lockout*</c>.
/// </summary>
public sealed class LockoutService
{
    public const int DefaultMaxFailures = 5;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(15);

    private readonly DbContext _db;

    public LockoutService(DbContext db)
    {
        _db = db;
    }

    public static int GetMaxFailures(IConfiguration configuration)
    {
        return int.TryParse(configuration["Auth:LockoutMaxFailures"], out var value) && value > 0
            ? value
            : DefaultMaxFailures;
    }

    public static TimeSpan GetWindow(IConfiguration configuration)
    {
        return int.TryParse(configuration["Auth:LockoutWindowMinutes"], out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : DefaultWindow;
    }

    public async Task<bool> IsLockedOutAsync(
        string username,
        string ipAddress,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var window = GetWindow(configuration);
        var max = GetMaxFailures(configuration);
        var cutoff = DateTimeOffset.UtcNow - window;

        var attempts = await _db.Set<LoginAttempt>()
            .Where(a => a.Username == username && a.IpAddress == ipAddress)
            .ToListAsync(cancellationToken);

        var failures = attempts.Count(a => a.At >= cutoff && !a.Succeeded);
        return failures >= max;
    }

    public async Task RecordSuccessAsync(
        string username,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        _db.Set<LoginAttempt>().Add(new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Username = username,
            IpAddress = ipAddress,
            Succeeded = true,
            At = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(
        string username,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        _db.Set<LoginAttempt>().Add(new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Username = username,
            IpAddress = ipAddress,
            Succeeded = false,
            At = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
