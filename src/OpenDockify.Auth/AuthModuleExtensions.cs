using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Auth.Services;
using Platform.Identity.Contracts;

namespace OpenDockify.Auth;

public static class AuthModuleExtensions
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<AccountService>();
        services.AddScoped<LockoutService>();
        services.AddScoped<RefreshTokenStore>();
        services.AddScoped<IdentityAuditHook>();
        services.AddSingleton<SessionStore>();
        services.AddScoped<PasswordRecoveryService>();
        services.AddScoped<TwoFactorService>();
        services.AddScoped<AdminStore>();
        services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<SessionStore>());
        services.AddSingleton<IIdentityAuditHook>(sp => new ScopedAuditHookAdapter(
            sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddScoped<Platform.Identity.Contracts.IRefreshTokenStore>(sp => sp.GetRequiredService<RefreshTokenStore>());
        services.AddScoped<Platform.Identity.Contracts.IPasswordRecoveryService>(sp => sp.GetRequiredService<PasswordRecoveryService>());
        services.AddScoped<Platform.Identity.Contracts.ITwoFactorService>(sp => sp.GetRequiredService<TwoFactorService>());
        services.AddScoped<Platform.Identity.Contracts.IRefreshTokenService>(sp => new Platform.Identity.Contracts.DefaultRefreshTokenService(
            sp.GetRequiredService<Platform.Identity.Contracts.IRefreshTokenStore>(),
            sp.GetService<IIdentityAuditHook>()));
        services.AddScoped<Platform.Identity.Contracts.IIdentityLifecycleCoordinator>(sp => new Platform.Identity.Contracts.DefaultIdentityLifecycleCoordinator(
            sp.GetRequiredService<Platform.Identity.Contracts.IRefreshTokenService>(),
            sp.GetRequiredService<Platform.Identity.Contracts.IPasswordRecoveryService>(),
            sp.GetRequiredService<Platform.Identity.Contracts.ITwoFactorService>(),
            new OpenDockifyAuthMissingImpersonationService()));
        return services;
    }
}

/// <summary>
/// Stand-in for the platform's missing impersonation service, used because
/// the platform package's internal marker type is not public.
/// </summary>
internal sealed class OpenDockifyAuthMissingImpersonationService : Platform.Identity.Contracts.IImpersonationService
{
    public ValueTask<Platform.Identity.Contracts.IdentityLifecycleResult<Platform.Identity.Contracts.ImpersonationGrant>> StartAsync(
        Platform.Identity.Contracts.ImpersonationAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Platform.Identity.Contracts.IdentityLifecycleResults.Failed<Platform.Identity.Contracts.ImpersonationGrant>(Platform.Identity.Contracts.IdentityLifecycleOutcome.PolicyDenied));
    }

    public ValueTask<Platform.Identity.Contracts.IdentityLifecycleOutcome> EndAsync(
        string grantId,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Platform.Identity.Contracts.IdentityLifecycleOutcome.PolicyDenied);
    }

    public ValueTask<Platform.Identity.Contracts.IdentityLifecycleResult<Platform.Identity.Contracts.ImpersonationContext>> GetActiveAsync(
        string callerSubjectId,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Platform.Identity.Contracts.IdentityLifecycleResults.Failed<Platform.Identity.Contracts.ImpersonationContext>(Platform.Identity.Contracts.IdentityLifecycleOutcome.PolicyDenied));
    }
}

/// <summary>
/// Resolves the <see cref="CurrentUserContext"/> for a request from the
/// validated JWT claims.
/// </summary>
public static class CurrentUserContextFactory
{
    public static CurrentUserContext FromClaims(System.Security.Claims.ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? principal.FindFirst("sub");
        var role = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
            ?? string.Empty;

        if (sub is null || !Guid.TryParse(sub.Value, out var userId))
        {
            throw new InvalidOperationException("Authenticated request is missing a valid sub claim.");
        }

        return new CurrentUserContext(userId, role);
    }
}
