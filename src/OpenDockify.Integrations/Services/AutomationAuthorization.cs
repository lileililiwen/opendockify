using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDockify.Integrations.Configuration;

namespace OpenDockify.Integrations.Services;

public static class AutomationAuthorization
{
    public const string SchemeName = "ServiceToken";

    /// <summary>Policy name for a required automation scope, e.g. "automation:documents:write".</summary>
    public static string PolicyFor(string scope)
    {
        return $"automation:{scope}";
    }

    /// <summary>Registers one policy per known scope; each requires the scope claim.</summary>
    public static void AddPolicies(AuthorizationOptions options)
    {
        foreach (var scope in AutomationScopes.All)
        {
            options.AddPolicy(
                PolicyFor(scope),
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(SchemeName)
                    .AddRequirements(new AutomationScopeRequirement(scope)));
        }
    }
}

/// <summary>Authorization requirement asserting the token carries the endpoint's scope.</summary>
public sealed class AutomationScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}

public sealed class AutomationScopeHandler : AuthorizationHandler<AutomationScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AutomationScopeRequirement requirement)
    {
        if (context.User.HasClaim(ClaimTypes.Role, "automation-token")
            && context.User.HasClaim("scope", requirement.Scope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Authenticates <c>Authorization: Bearer odk_...</c> service tokens for the
/// automation API. JWT bearer remains the default scheme everywhere else.
/// </summary>
public sealed class ServiceTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ServiceTokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (!authorization.StartsWith(bearer, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var credential = authorization[bearer.Length..].Trim();
        if (!credential.StartsWith(ServiceTokenService.ClearTokenPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var authentication = await tokens.AuthenticateAsync(credential, Context.RequestAborted);
        if (authentication is null)
        {
            return AuthenticateResult.Fail("Invalid, expired, or revoked service token.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authentication.OwnerId.ToString()),
            new("token_id", authentication.TokenId.ToString()),
            new(ClaimTypes.Role, "automation-token"),
        };
        claims.AddRange(authentication.Scopes.Select(scope => new Claim("scope", scope)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = $"{Scheme.Name} realm=\"opendockify-automation\"";
        return base.HandleChallengeAsync(properties);
    }
}
