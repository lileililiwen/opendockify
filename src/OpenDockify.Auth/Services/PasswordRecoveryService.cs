using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenDockify.Auth.Models;
using PlatformLifecycleOutcome = Platform.Identity.Contracts.IdentityLifecycleOutcome;
using PlatformLifecycleResults = Platform.Identity.Contracts.IdentityLifecycleResults;
using PlatformPasswordRecoveryChallenge = Platform.Identity.Contracts.PasswordRecoveryChallenge;
using PlatformPasswordRecoveryService = Platform.Identity.Contracts.IPasswordRecoveryService;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Application-owned <see cref="PlatformPasswordRecoveryService"/> backed by the
/// <c>RecoveryTokens</c> table. Initiate is enumeration-safe: a record is
/// always written, and the same <c>202</c> response is returned whether or
/// not a matching user exists. The cleartext recovery code is delivered
/// out-of-band (logged to the console when SMTP is not configured — this
/// is a self-hosted application per Agents.md).
/// </summary>
public sealed class PasswordRecoveryService : PlatformPasswordRecoveryService
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(30);

    private static readonly Action<ILogger, string, string, string, Exception?> _logRecoveryInitiated =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1001, nameof(InitiateAsync)),
            "Password recovery initiated for user {Username}. Challenge: {ChallengeId}. Code: {Code}");

    private readonly DbContext _db;
    private readonly AccountService _account;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly TimeProvider _time;
    private readonly ILogger<PasswordRecoveryService> _logger;
    private readonly IConfiguration _configuration;

    public PasswordRecoveryService(
        DbContext db,
        AccountService account,
        TimeProvider time,
        ILogger<PasswordRecoveryService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _account = account;
        _time = time;
        _logger = logger;
        _configuration = configuration;
    }

    public TimeSpan GetLifetime()
    {
        return int.TryParse(_configuration["Auth:RecoveryTokenMinutes"], out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : DefaultLifetime;
    }

    public async ValueTask<Platform.Identity.Contracts.IdentityLifecycleResult<PlatformPasswordRecoveryChallenge>> InitiateAsync(
        string subjectIdentifier,
        CancellationToken cancellationToken = default)
    {
        // Always create a row so timing/response shape is identical with or without
        // a matching account. The challenge id is unique by index; the code is
        // hashed at rest.
        var now = _time.GetUtcNow();
        var challengeId = Guid.NewGuid().ToString("N");
        var code = TokenObfuscator.NewRecoveryCode();
        var user = await _account.FindByUsernameAsync(subjectIdentifier.Trim(), cancellationToken);

        _db.Set<Models.RecoveryToken>().Add(new Models.RecoveryToken
        {
            Id = Guid.NewGuid(),
            UserId = user?.Id,
            ChallengeId = challengeId,
            CodeHash = TokenObfuscator.Sha256Hex(code),
            IssuedAt = now,
            ExpiresAt = now.Add(GetLifetime()),
        });
        await _db.SaveChangesAsync(cancellationToken);

        if (user is not null)
        {
            // Self-hosted deployment: log to console when SMTP is not configured.
            // The code is for the operator/dev only; never written to the database in cleartext.
            _logRecoveryInitiated(_logger, user.Username, challengeId, code, null);
        }

        return PlatformLifecycleResults.Success(new PlatformPasswordRecoveryChallenge(
            ChallengeId: challengeId,
            ExpiresAt: now.Add(GetLifetime())));
    }

    public async ValueTask<Platform.Identity.Contracts.IdentityLifecycleResult<bool>> CompleteAsync(
        string challengeId,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(code) || string.IsNullOrEmpty(newPassword))
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.InvalidRequest);
        }

        if (newPassword.Length < 8)
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.PolicyDenied);
        }

        var now = _time.GetUtcNow();
        var record = await _db.Set<Models.RecoveryToken>()
            .FirstOrDefaultAsync(r => r.ChallengeId == challengeId, cancellationToken);
        if (record == null)
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.InvalidHandle);
        }

        if (record.ConsumedAt != null)
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.Replayed);
        }

        if (record.ExpiresAt <= now)
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.Expired);
        }

        var presentedHash = TokenObfuscator.Sha256Hex(code);
        if (!CryptographicEquals(presentedHash, record.CodeHash))
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.PolicyDenied);
        }

        if (record.UserId == null)
        {
            // No matching user — enumeration-safe: treat as success without writing anything.
            record.ConsumedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return PlatformLifecycleResults.Success(true);
        }

        var user = await _account.FindByIdAsync(record.UserId.Value, cancellationToken);
        if (user == null)
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.PreconditionNotMet);
        }

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        record.ConsumedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        // Revoke all refresh tokens for the user.
        var store = new RefreshTokenStore(_db, _time);
        await store.RevokeUserFamiliesAsync(user.Id, "password_changed", cancellationToken);

        return PlatformLifecycleResults.Success(true);
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }
}
