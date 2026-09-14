using System.Collections.Concurrent;
using Platform.Identity.Contracts;

namespace OpenDockify.Auth.Services;

/// <summary>
/// In-memory session store. The platform contracts only require the
/// platform-issued session id; refresh-token replay-detection is the
/// authoritative authorization gate, so sessions are kept minimal and
/// non-durable (lost on restart is acceptable — clients re-authenticate).
/// </summary>
public sealed class SessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, (string SubjectId, DateTimeOffset ExpiresAt)> _sessions = new();

    public ValueTask<IdentityProviderResult<IdentitySession>> CreateAsync(
        string subjectId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = (subjectId, expiresAt);
        return ValueTask.FromResult(IdentityProviderResults.Success(new IdentitySession(sessionId, subjectId, expiresAt)));
    }

    public ValueTask<IdentityProviderResult<bool>> RevokeAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var revoked = _sessions.TryRemove(sessionId, out _);
        return ValueTask.FromResult(revoked
            ? IdentityProviderResults.Success(true)
            : IdentityProviderResults.Failed<bool>(IdentityFailureReason.InvalidRequest));
    }
}
