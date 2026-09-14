using Microsoft.EntityFrameworkCore;
using OpenDockify.Auth.Models;
using PlatformLifecycleOutcome = Platform.Identity.Contracts.IdentityLifecycleOutcome;
using PlatformLifecycleResults = Platform.Identity.Contracts.IdentityLifecycleResults;
using PlatformRefreshRotationResult = Platform.Identity.Contracts.IdentityLifecycleResult<Platform.Identity.Contracts.RefreshTokenRotation>;
using PlatformRefreshToken = Platform.Identity.Contracts.RefreshToken;
using PlatformRefreshTokenResult = Platform.Identity.Contracts.IdentityLifecycleResult<Platform.Identity.Contracts.RefreshToken>;
using PlatformRefreshTokenRotation = Platform.Identity.Contracts.RefreshTokenRotation;
using PlatformRefreshTokenStore = Platform.Identity.Contracts.IRefreshTokenStore;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Application-owned <see cref="PlatformRefreshTokenStore"/> implementation backed
/// by the <c>RefreshTokens</c> table. Issues opaque 256-bit handles, hashes
/// them with SHA-256 at rest, and enforces single-use semantics for
/// rotation. Reuse of an already-consumed handle revokes the entire token
/// family (a "session" from the client's perspective).
/// </summary>
public sealed class RefreshTokenStore : PlatformRefreshTokenStore
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(14);

    private readonly DbContext _db;
    private readonly TimeProvider _time;

    public RefreshTokenStore(DbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async ValueTask<PlatformRefreshTokenResult> IssueAsync(
        string subjectId,
        string sessionId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(subjectId, out var userId))
        {
            return PlatformLifecycleResults.Failed<PlatformRefreshToken>(PlatformLifecycleOutcome.InvalidRequest);
        }

        var handle = TokenObfuscator.NewHandle();
        var entity = new Models.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = sessionId,
            TokenHash = TokenObfuscator.Sha256Hex(handle),
            IssuedAt = _time.GetUtcNow(),
            ExpiresAt = expiresAt,
        };
        _db.Set<Models.RefreshToken>().Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return PlatformLifecycleResults.Success(new PlatformRefreshToken(
            Handle: handle,
            SubjectId: subjectId,
            SessionId: sessionId,
            IssuedAt: entity.IssuedAt,
            ExpiresAt: entity.ExpiresAt));
    }

    public async ValueTask<PlatformRefreshRotationResult> ConsumeAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return PlatformLifecycleResults.Failed<PlatformRefreshTokenRotation>(PlatformLifecycleOutcome.InvalidHandle);
        }

        var hash = TokenObfuscator.Sha256Hex(handle);
        var now = _time.GetUtcNow();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await _db.Set<Models.RefreshToken>()
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);

        if (existing == null)
        {
            return PlatformLifecycleResults.Failed<PlatformRefreshTokenRotation>(PlatformLifecycleOutcome.InvalidHandle);
        }

        if (existing.RevokedAt != null)
        {
            // Reuse: revoke the entire family.
            await RevokeFamilyAsync(existing.FamilyId, "reused", cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return PlatformLifecycleResults.Failed<PlatformRefreshTokenRotation>(PlatformLifecycleOutcome.Replayed);
        }

        if (existing.ConsumedAt != null)
        {
            // Race: another concurrent caller won the consume. Treat as replay.
            await RevokeFamilyAsync(existing.FamilyId, "replayed", cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return PlatformLifecycleResults.Failed<PlatformRefreshTokenRotation>(PlatformLifecycleOutcome.Replayed);
        }

        if (existing.ExpiresAt <= now)
        {
            return PlatformLifecycleResults.Failed<PlatformRefreshTokenRotation>(PlatformLifecycleOutcome.Expired);
        }

        // Linearizable consume: mark consumed and rotate a new handle in the same family.
        existing.ConsumedAt = now;

        var newHandle = TokenObfuscator.NewHandle();
        var replacement = new Models.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            FamilyId = existing.FamilyId,
            TokenHash = TokenObfuscator.Sha256Hex(newHandle),
            ParentId = existing.Id,
            IssuedAt = now,
            ExpiresAt = now.Add(DefaultLifetime),
        };
        _db.Set<Models.RefreshToken>().Add(replacement);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return PlatformLifecycleResults.Success(new PlatformRefreshTokenRotation(
            Token: newHandle,
            Handle: newHandle,
            ExpiresAt: replacement.ExpiresAt));
    }

    public async ValueTask<PlatformLifecycleOutcome> RevokeAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return PlatformLifecycleOutcome.InvalidHandle;
        }

        var hash = TokenObfuscator.Sha256Hex(handle);
        var existing = await _db.Set<Models.RefreshToken>()
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);
        if (existing == null)
        {
            return PlatformLifecycleOutcome.InvalidHandle;
        }

        if (existing.RevokedAt != null)
        {
            return PlatformLifecycleOutcome.Revoked;
        }

        await RevokeFamilyAsync(existing.FamilyId, "logout", cancellationToken);
        return PlatformLifecycleOutcome.Succeeded;
    }

    /// <summary>
    /// Revokes every token (consumed or not) in the supplied family. Used by
    /// reuse detection, logout, and password-change flows.
    /// </summary>
    public async Task RevokeUserFamiliesAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var families = await _db.Set<Models.RefreshToken>()
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .Select(r => r.FamilyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var family in families)
        {
            await RevokeFamilyAsync(family, reason, cancellationToken);
        }
    }

    private async Task RevokeFamilyAsync(string familyId, string reason, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var rows = await _db.Set<Models.RefreshToken>()
            .Where(r => r.FamilyId == familyId && r.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.RevokedAt = now;
            row.RevokeReason = reason;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
