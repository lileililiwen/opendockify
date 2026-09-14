using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Auth;
using OpenDockify.Auth.Models;
using OpenDockify.Auth.Services;
using IPasswordRecoveryService = Platform.Identity.Contracts.IPasswordRecoveryService;
using IRefreshTokenService = Platform.Identity.Contracts.IRefreshTokenService;
using ISessionStore = Platform.Identity.Contracts.ISessionStore;
using ITwoFactorService = Platform.Identity.Contracts.ITwoFactorService;
using PlatformIdentityAuditEvent = Platform.Identity.Contracts.IdentityAuditEvent;
using PlatformLifecycleOutcome = Platform.Identity.Contracts.IdentityLifecycleOutcome;

namespace OpenDockify.Api;

/// <summary>
/// Auth endpoints: register, login, refresh, logout, change-password,
/// recovery, 2FA, plus the current-user query. Endpoint groups keep the
/// Minimal API shell tidy; each domain lands its own group here (composition
/// root).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IConfiguration configuration,
            AccountService account,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!AuthSecurityOptions.IsRegistrationAllowed(configuration))
            {
                return Results.Json(
                    new { error = "Public registration is disabled by the server administrator." },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await account.RegisterAsync(
                request.Username, request.Password, request.DisplayName ?? string.Empty, UserRole.Regular, ct);

            if (!result.Succeeded)
            {
                return Results.Json(
                    new { error = result.Error },
                    statusCode: result.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest);
            }

            var (accessToken, refreshToken) = await IssueSessionAsync(http, result.User!, ct);
            return Results.Ok(new AuthResponse(result.User!.Id, result.User.Username, result.User.Role.ToString(), accessToken, refreshToken));
        }).RequireRateLimiting(AuthSecurityOptions.RegistrationPolicyName);

        group.MapPost("/login", async (
            LoginRequest request,
            IConfiguration configuration,
            AccountService account,
            JwtTokenService tokens,
            LockoutService lockout,
            HttpContext http,
            ITwoFactorService twoFactor,
            CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var username = request.Username?.Trim() ?? string.Empty;

            if (await lockout.IsLockedOutAsync(username, ip, configuration, ct))
            {
                return Results.Json(
                    new { error = "Too many failed attempts. Try again later." },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var user = await account.ValidateCredentialsAsync(username, request.Password ?? string.Empty, ct);
            if (user is null)
            {
                await lockout.RecordFailureAsync(username, ip, ct);
                return Results.Json(new { error = "Invalid username or password." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (user.IsDisabled)
            {
                await lockout.RecordFailureAsync(username, ip, ct);
                return Results.Json(new { error = "Invalid username or password." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            await lockout.RecordSuccessAsync(username, ip, ct);

            // If 2FA is enabled, issue a challenge instead of tokens. The client
            // completes login at /2fa/verify with the TOTP code.
            var db = http.RequestServices.GetRequiredService<DbContext>();
            var has2fa = await db.Set<TwoFactorSecret>()
                .AnyAsync(t => t.UserId == user.Id && t.IsEnabled, ct);
            if (has2fa)
            {
                var challenge = await twoFactor.IssueAsync(user.Id.ToString(), ct);
                if (!challenge.Succeeded)
                {
                    return Results.Json(new { error = "Could not start two-factor verification." }, statusCode: StatusCodes.Status500InternalServerError);
                }
                return Results.Ok(new LoginResponse(
                    Mode: "2fa-required",
                    UserId: user.Id,
                    Username: user.Username,
                    Role: user.Role.ToString(),
                    Token: null,
                    RefreshToken: null,
                    ChallengeId: challenge.Value!.ChallengeId));
            }

            var (accessToken, refreshToken) = await IssueSessionAsync(http, user, ct);
            return Results.Ok(new LoginResponse(
                Mode: "authenticated",
                UserId: user.Id,
                Username: user.Username,
                Role: user.Role.ToString(),
                Token: accessToken,
                RefreshToken: refreshToken,
                ChallengeId: null));
        }).RequireRateLimiting(AuthSecurityOptions.LoginPolicyName);

        group.MapPost("/refresh", async (
            RefreshRequest request,
            HttpContext http,
            IRefreshTokenService refreshTokens,
            JwtTokenService tokens,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.Json(new { error = "Refresh token is required." }, statusCode: StatusCodes.Status400BadRequest);
            }

            // Resolve the subject from the presented handle BEFORE consuming it:
            // rotation returns a new opaque handle, not the subject id.
            var user = await ResolveSubjectAsync(http, request.RefreshToken, ct);
            if (user is null || user.IsDisabled)
            {
                return Results.Json(new { error = "Refresh token rejected." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await refreshTokens.RotateAsync(request.RefreshToken, ct);
            if (!result.Succeeded)
            {
                return Results.Json(
                    new { error = result.Outcome == PlatformLifecycleOutcome.Replayed ? "Refresh token reused." : "Refresh token rejected." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var accessToken = tokens.IssueToken(user.Id, user.Role.ToString());
            return Results.Ok(new AuthResponse(user.Id, user.Username, user.Role.ToString(), accessToken, result.Value!.Handle));
        });

        group.MapPost("/logout", async (
            RefreshRequest request,
            IRefreshTokenService refreshTokens,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.NoContent();
            }

            await refreshTokens.RevokeAsync(request.RefreshToken, ct);
            return Results.NoContent();
        });

        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            System.Security.Claims.ClaimsPrincipal principal,
            AccountService account,
            RefreshTokenStore refreshStore,
            CancellationToken ct) =>
        {
            var ctx = CurrentUserContextFactory.FromClaims(principal);
            var user = await account.FindByIdAsync(ctx.UserId, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
            {
                return Results.Json(new { error = "Current and new password are required." }, statusCode: StatusCodes.Status400BadRequest);
            }

            if (request.NewPassword.Length < 8)
            {
                return Results.Json(new { error = "New password must be at least 8 characters." }, statusCode: StatusCodes.Status400BadRequest);
            }

            if (!await account.VerifyCurrentPasswordAsync(user, request.CurrentPassword, ct))
            {
                return Results.Json(new { error = "Current password is incorrect." }, statusCode: StatusCodes.Status400BadRequest);
            }

            var set = await account.SetPasswordAsync(user, request.NewPassword, ct);
            if (!set.Succeeded)
            {
                return Results.Json(new { error = set.Error }, statusCode: StatusCodes.Status400BadRequest);
            }

            await refreshStore.RevokeUserFamiliesAsync(user.Id, "password_changed", ct);
            return Results.Ok(new { user.Id, user.Username });
        }).RequireAuthorization();

        group.MapPost("/recovery/start", async (
            RecoveryStartRequest request,
            IPasswordRecoveryService recovery,
            CancellationToken ct) =>
        {
            // Enumeration-safe: same 202 whether or not the user exists.
            await recovery.InitiateAsync(request.Username ?? string.Empty, ct);
            return Results.Accepted();
        }).RequireRateLimiting(AuthSecurityOptions.RecoveryPolicyName);

        group.MapPost("/recovery/complete", async (
            RecoveryCompleteRequest request,
            IPasswordRecoveryService recovery,
            CancellationToken ct) =>
        {
            var result = await recovery.CompleteAsync(
                request.ChallengeId ?? string.Empty,
                request.Code ?? string.Empty,
                request.NewPassword ?? string.Empty,
                ct);
            if (!result.Succeeded)
            {
                return Results.Json(
                    new { error = "Recovery failed." },
                    statusCode: StatusCodes.Status400BadRequest);
            }
            return Results.NoContent();
        }).RequireRateLimiting(AuthSecurityOptions.RecoveryPolicyName);

        group.MapPost("/2fa/start", async (
            System.Security.Claims.ClaimsPrincipal principal,
            ITwoFactorService twoFactor,
            CancellationToken ct) =>
        {
            var ctx = CurrentUserContextFactory.FromClaims(principal);
            var result = await twoFactor.IssueAsync(ctx.UserId.ToString(), ct);
            if (!result.Succeeded)
            {
                return Results.Json(new { error = "Two-factor is not enabled for this account." }, statusCode: StatusCodes.Status400BadRequest);
            }
            return Results.Ok(new { challengeId = result.Value!.ChallengeId, expiresAt = result.Value.ExpiresAt });
        }).RequireAuthorization();

        group.MapPost("/2fa/verify", async (
            TwoFactorVerifyRequest request,
            HttpContext http,
            ITwoFactorService twoFactor,
            JwtTokenService tokens,
            AccountService account,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ChallengeId) || string.IsNullOrWhiteSpace(request.Code))
            {
                return Results.Json(new { error = "Challenge id and code are required." }, statusCode: StatusCodes.Status400BadRequest);
            }

            // Anonymous: the challenge binds the subject (issued at login).
            var user = await ResolveChallengeSubjectAsync(http, request.ChallengeId, ct);
            if (user is null || user.IsDisabled)
            {
                return Results.Json(new { error = "Two-factor verification failed." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await twoFactor.VerifyAsync(request.ChallengeId, request.Code, ct);
            if (!result.Succeeded)
            {
                return Results.Json(new { error = "Two-factor verification failed." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var (accessToken, refreshToken) = await IssueSessionAsync(http, user, ct);
            return Results.Ok(new AuthResponse(user.Id, user.Username, user.Role.ToString(), accessToken, refreshToken));
        });

        group.MapGet("/me", async (
            System.Security.Claims.ClaimsPrincipal principal,
            AccountService account,
            CancellationToken ct) =>
        {
            var ctx = CurrentUserContextFactory.FromClaims(principal);
            var user = await account.FindByIdAsync(ctx.UserId, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new { user.Id, user.Username, user.DisplayName, Role = user.Role.ToString(), ctx.IsAdministrator });
        }).RequireAuthorization();

        return endpoints;
    }

    private static async Task<(string AccessToken, string RefreshToken)> IssueSessionAsync(
        HttpContext http,
        User user,
        CancellationToken ct)
    {
        var tokens = http.RequestServices.GetRequiredService<JwtTokenService>();
        var sessionStore = http.RequestServices.GetRequiredService<ISessionStore>();
        var refreshTokens = http.RequestServices.GetRequiredService<IRefreshTokenService>();

        var accessToken = tokens.IssueToken(user.Id, user.Role.ToString());
        var sessionLifetime = TimeSpan.FromDays(14);
        var session = await sessionStore.CreateAsync(user.Id.ToString(), DateTimeOffset.UtcNow.Add(sessionLifetime), ct);
        var issued = await refreshTokens.IssueAsync(
            user.Id.ToString(),
            session.Value!.SessionId,
            DateTimeOffset.UtcNow.Add(sessionLifetime),
            ct);
        return (accessToken, issued.Value!.Handle);
    }

    private static async Task<User?> ResolveChallengeSubjectAsync(HttpContext http, string challengeId, CancellationToken ct)
    {
        // The login-issued challenge binds the subject; verify is anonymous.
        var db = http.RequestServices.GetRequiredService<DbContext>();
        var row = await db.Set<TwoFactorChallenge>()
            .FirstOrDefaultAsync(c => c.ChallengeId == challengeId, ct);
        if (row == null)
        {
            return null;
        }
        return await db.Set<User>().FirstOrDefaultAsync(u => u.Id == row.UserId, ct);
    }

    private static async Task<User?> ResolveSubjectAsync(HttpContext http, string refreshHandle, CancellationToken ct)
    {
        // Best-effort fallback: hash the handle to the database and look up the user. The
        // platform's RefreshToken contract does not return the subject id, so this is
        // the only way to issue a new access JWT after rotation. The store returns the
        // handle (not the subject) on rotation, so we re-resolve from the database.
        var db = http.RequestServices.GetRequiredService<DbContext>();
        var hash = AccountService.Sha256Hex(refreshHandle);
        // TokenHash is unique: no ordering needed (SQLite cannot order
        // DateTimeOffset in a translated query).
        var row = await db.Set<RefreshToken>()
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (row == null)
        {
            return null;
        }
        return await db.Set<User>().FirstOrDefaultAsync(u => u.Id == row.UserId, ct);
    }
}

public sealed record RegisterRequest(string Username, string Password, string? DisplayName);

public sealed record LoginRequest(string Username, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record RecoveryStartRequest(string Username);

public sealed record RecoveryCompleteRequest(string ChallengeId, string Code, string NewPassword);

public sealed record TwoFactorVerifyRequest(string ChallengeId, string Code);

public sealed record AuthResponse(
    Guid Id,
    string Username,
    string Role,
    string Token,
    string? RefreshToken = null);

public sealed record LoginResponse(
    string Mode,
    Guid UserId,
    string Username,
    string Role,
    string? Token,
    string? RefreshToken,
    string? ChallengeId);
