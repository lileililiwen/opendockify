using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenDockify.Auth;
using OpenDockify.Integrations.Configuration;
using OpenDockify.Integrations.Models;
using OpenDockify.Integrations.Services;

namespace OpenDockify.Api;

/// <summary>
/// User-owned integration management (JWT): service token lifecycle, webhook
/// subscriptions, delivery inspection, and manual retry. Secrets are shown
/// exactly once at creation and never again.
/// </summary>
public static class IntegrationManagementEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/integrations").RequireAuthorization();

        group.MapGet("/tokens", async (HttpContext http, ServiceTokenService tokens, CancellationToken ct) =>
            Results.Ok(await tokens.ListAsync(CurrentUserId(http), ct)));

        group.MapPost("/tokens", async (
            HttpContext http,
            CreateTokenRequest request,
            ServiceTokenService tokens,
            IOptions<IntegrationsOptions> options,
            CancellationToken ct) =>
        {
            if (request.Scopes is not { Count: > 0 } || request.Scopes.Any(s => !AutomationScopes.IsValid(s)))
            {
                return Results.Json(new { error = "At least one valid scope is required." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await using var db = http.RequestServices.GetRequiredService<DbContext>();
            var count = await db.Set<ServiceToken>().CountAsync(x => x.OwnerId == CurrentUserId(http), ct);
            if (count >= options.Value.GetMaxTokensPerOwner())
            {
                return Results.Json(new { error = $"At most {options.Value.GetMaxTokensPerOwner()} tokens may exist per owner." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var issuance = await tokens.CreateAsync(
                    CurrentUserId(http),
                    request.Name ?? "token",
                    request.Scopes,
                    TimeSpan.FromDays(Math.Clamp(request.ExpiresInDays, 1, 3650)),
                    ct);
                return Results.Json(
                    new
                    {
                        token = new
                        {
                            issuance.Token.Id,
                            issuance.Token.Name,
                            issuance.Token.Prefix,
                            issuance.Token.Scopes,
                            issuance.Token.CreatedAtUtc,
                            issuance.Token.ExpiresAtUtc,
                        },
                        // Shown exactly once; the server stores only a verifier.
                        clearToken = issuance.ClearToken,
                    },
                    statusCode: StatusCodes.Status201Created);
            }
            catch (ArgumentException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        group.MapDelete("/tokens/{id:guid}", async (
            HttpContext http,
            Guid id,
            ServiceTokenService tokens,
            CancellationToken ct) =>
            await tokens.RevokeAsync(CurrentUserId(http), id, ct)
                ? Results.NoContent()
                : Results.Json(new { error = "Token not found." }, statusCode: StatusCodes.Status404NotFound));

        group.MapGet("/webhooks/subscriptions", async (HttpContext http, DbContext db, CancellationToken ct) =>
            Results.Ok(await db.Set<WebhookSubscription>().AsNoTracking()
                .Where(x => x.OwnerId == CurrentUserId(http))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new WebhookSubscriptionView(x.Id, x.Url, x.EventTypes, x.IsActive, x.CreatedAtUtc))
                .ToListAsync(ct)));

        group.MapPost("/webhooks/subscriptions", async (
            HttpContext http,
            CreateSubscriptionRequest request,
            DbContext db,
            WebhookRoutePlanner planner,
            IOptions<IntegrationsOptions> options,
            CancellationToken ct) =>
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps)
            {
                return Results.Json(new { error = "The subscription URL must be absolute HTTPS." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (request.EventTypes is not { Count: > 0 } || request.EventTypes.Any(t => !AutomationEvents.IsValid(t)))
            {
                return Results.Json(new { error = "At least one valid event type is required." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Refuse destinations that outbound-safety would block on every attempt.
            var plan = await planner.PlanAsync(url, ct);
            if (!plan.Allowed)
            {
                return Results.Json(new { error = $"The destination was refused by outbound-safety checks ({plan.BlockedReason})." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var ownerId = CurrentUserId(http);
            var count = await db.Set<WebhookSubscription>().CountAsync(x => x.OwnerId == ownerId, ct);
            if (count >= options.Value.GetMaxSubscriptionsPerOwner())
            {
                return Results.Json(new { error = $"At most {options.Value.GetMaxSubscriptionsPerOwner()} subscriptions may exist per owner." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var secret = $"whsec_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=')}";
            var subscription = new WebhookSubscription
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Url = url.ToString(),
                Secret = secret,
                EventTypes = string.Join(',', request.EventTypes.Distinct(StringComparer.Ordinal)),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.Set<WebhookSubscription>().Add(subscription);
            await db.SaveChangesAsync(ct);

            return Results.Json(
                new
                {
                    subscription = new WebhookSubscriptionView(
                        subscription.Id, subscription.Url, subscription.EventTypes, subscription.IsActive, subscription.CreatedAtUtc),
                    // Shown exactly once so the receiver can verify signatures.
                    secret,
                },
                statusCode: StatusCodes.Status201Created);
        });

        group.MapDelete("/webhooks/subscriptions/{id:guid}", async (
            HttpContext http,
            Guid id,
            DbContext db,
            CancellationToken ct) =>
        {
            var rows = await db.Set<WebhookSubscription>()
                .Where(x => x.Id == id && x.OwnerId == CurrentUserId(http))
                .ExecuteDeleteAsync(ct);
            return rows > 0 ? Results.NoContent() : Results.NotFound(new { error = "Subscription not found." });
        });

        group.MapGet("/webhooks/deliveries", async (
            HttpContext http,
            WebhookDeliveryService deliveries,
            Guid? subscriptionId,
            int limit = 50,
            CancellationToken ct = default) =>
            Results.Ok(await deliveries.ListDeliveriesAsync(CurrentUserId(http), subscriptionId, limit, ct)));

        group.MapPost("/webhooks/deliveries/{id:guid}/retry", async (
            HttpContext http,
            Guid id,
            WebhookDeliveryService deliveries,
            CancellationToken ct) =>
            await deliveries.RetryNowAsync(CurrentUserId(http), id, ct)
                ? Results.Accepted()
                : Results.Json(new { error = "Delivery not found." }, statusCode: StatusCodes.Status404NotFound));

        return endpoints;
    }

    private static Guid CurrentUserId(HttpContext http)
    {
        return CurrentUserContextFactory.FromClaims(http.User).UserId;
    }
}

public sealed record CreateTokenRequest(string? Name, List<string>? Scopes, int ExpiresInDays);

public sealed record CreateSubscriptionRequest(string Url, List<string>? EventTypes);
