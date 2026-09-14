using Microsoft.Extensions.DependencyInjection;
using PlatformAuditEvent = Platform.Identity.Contracts.IdentityAuditEvent;
using PlatformAuditHook = Platform.Identity.Contracts.IIdentityAuditHook;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Singleton adapter that forwards audit events to the scoped
/// <see cref="IdentityAuditHook"/> by creating a fresh DI scope per call.
/// The platform's lifecycle services capture the audit hook as a singleton;
/// we keep our application-owned implementation scoped (because it depends
/// on the per-request <c>DbContext</c>) and bridge the two lifetimes here.
/// </summary>
public sealed class ScopedAuditHookAdapter : PlatformAuditHook
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedAuditHookAdapter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async ValueTask RecordAsync(PlatformAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var hook = scope.ServiceProvider.GetRequiredService<IdentityAuditHook>();
        await hook.RecordAsync(auditEvent, cancellationToken);
    }
}
