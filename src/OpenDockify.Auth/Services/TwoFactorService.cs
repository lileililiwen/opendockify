using Microsoft.EntityFrameworkCore;
using OpenDockify.Auth.Models;
using PlatformLifecycleOutcome = Platform.Identity.Contracts.IdentityLifecycleOutcome;
using PlatformLifecycleResults = Platform.Identity.Contracts.IdentityLifecycleResults;
using PlatformTwoFactorChallenge = Platform.Identity.Contracts.TwoFactorChallenge;
using PlatformTwoFactorService = Platform.Identity.Contracts.ITwoFactorService;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Application-owned <see cref="PlatformTwoFactorService"/>. The challenge is the
/// platform-issued token the client re-presents at the verify endpoint; the
/// code is the user-entered TOTP value. Issuance requires the subject to
/// have an enabled enrolment; otherwise the platform outcome is
/// <see cref="PlatformLifecycleOutcome.PreconditionNotMet"/>.
/// </summary>
public sealed class TwoFactorService : PlatformTwoFactorService
{
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private static readonly string[] _defaultChannels = ["totp", "recovery_code"];

    private readonly DbContext _db;
    private readonly TimeProvider _time;

    public TwoFactorService(DbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async ValueTask<Platform.Identity.Contracts.IdentityLifecycleResult<PlatformTwoFactorChallenge>> IssueAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(subjectId, out var userId))
        {
            return PlatformLifecycleResults.Failed<PlatformTwoFactorChallenge>(PlatformLifecycleOutcome.InvalidRequest);
        }

        var enrolment = await _db.Set<Models.TwoFactorSecret>()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsEnabled, cancellationToken);
        if (enrolment == null)
        {
            return PlatformLifecycleResults.Failed<PlatformTwoFactorChallenge>(PlatformLifecycleOutcome.PreconditionNotMet);
        }

        var now = _time.GetUtcNow();
        var challengeId = Guid.NewGuid().ToString("N");
        _db.Set<Models.TwoFactorChallenge>().Add(new Models.TwoFactorChallenge
        {
            Id = Guid.NewGuid(),
            ChallengeId = challengeId,
            UserId = userId,
            IssuedAt = now,
            ExpiresAt = now.Add(ChallengeLifetime),
        });
        await _db.SaveChangesAsync(cancellationToken);

        return PlatformLifecycleResults.Success(new PlatformTwoFactorChallenge(
            ChallengeId: challengeId,
            ExpiresAt: now.Add(ChallengeLifetime),
            Channels: _defaultChannels));
    }

    public async ValueTask<Platform.Identity.Contracts.IdentityLifecycleResult<bool>> VerifyAsync(
        string challengeId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(code))
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.InvalidRequest);
        }

        var now = _time.GetUtcNow();
        var record = await _db.Set<Models.TwoFactorChallenge>()
            .FirstOrDefaultAsync(c => c.ChallengeId == challengeId, cancellationToken);
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

        var enrolment = await _db.Set<Models.TwoFactorSecret>()
            .FirstOrDefaultAsync(t => t.UserId == record.UserId && t.IsEnabled, cancellationToken);
        if (enrolment == null)
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.PreconditionNotMet);
        }

        var isValidTotp = TotpValidator.Validate(enrolment.Secret, code);
        var isValidRecovery = !isValidTotp && IsRecoveryCode(code, enrolment.RecoveryCodesHash);
        if (!isValidTotp && !isValidRecovery)
        {
            return PlatformLifecycleResults.Failed<bool>(PlatformLifecycleOutcome.PolicyDenied);
        }

        if (isValidRecovery)
        {
            enrolment.RecoveryCodesHash = ConsumeRecoveryCode(code, enrolment.RecoveryCodesHash);
        }

        record.ConsumedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return PlatformLifecycleResults.Success(true);
    }

    private static bool IsRecoveryCode(string code, string hashes)
    {
        if (string.IsNullOrWhiteSpace(hashes))
        {
            return false;
        }

        var presented = TokenObfuscator.Sha256Hex(code);
        return hashes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(stored => CryptographicEquals(presented, stored));
    }

    private static string ConsumeRecoveryCode(string code, string hashes)
    {
        var presented = TokenObfuscator.Sha256Hex(code);
        var remaining = hashes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(stored => !CryptographicEquals(presented, stored));
        return string.Join(',', remaining);
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
