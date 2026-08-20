using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Auth.Services;

namespace OpenDockify.Auth;

public static class AuthModuleExtensions
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<AccountService>();
        return services;
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
