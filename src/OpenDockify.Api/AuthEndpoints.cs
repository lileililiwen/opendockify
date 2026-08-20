using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Auth;
using OpenDockify.Auth.Models;
using OpenDockify.Auth.Services;

namespace OpenDockify.Api;

/// <summary>
/// Auth endpoints: register, login, me. Endpoint groups keep the Minimal API
/// shell tidy; each domain lands its own group here (composition root).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            AccountService account,
            JwtTokenService tokens,
            CancellationToken ct) =>
        {
            var result = await account.RegisterAsync(
                request.Username, request.Password, request.DisplayName ?? string.Empty, UserRole.Regular, ct);

            if (!result.Succeeded)
            {
                return Results.Json(
                    new { error = result.Error },
                    statusCode: result.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest);
            }

            var token = tokens.IssueToken(result.User!.Id, result.User.Role.ToString());
            return Results.Ok(new AuthResponse(result.User.Id, result.User.Username, result.User.Role.ToString(), token));
        });

        group.MapPost("/login", async (
            LoginRequest request,
            AccountService account,
            JwtTokenService tokens,
            CancellationToken ct) =>
        {
            var user = await account.ValidateCredentialsAsync(request.Username, request.Password, ct);
            if (user is null)
            {
                return Results.Json(new { error = "Invalid username or password." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var token = tokens.IssueToken(user.Id, user.Role.ToString());
            return Results.Ok(new AuthResponse(user.Id, user.Username, user.Role.ToString(), token));
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
}

public sealed record RegisterRequest(string Username, string Password, string? DisplayName);

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResponse(Guid Id, string Username, string Role, string Token);
