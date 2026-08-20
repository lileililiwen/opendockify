using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenDockify.SystemConfig.Services;

namespace OpenDockify.Api;

/// <summary>
/// Admin-only system settings surface. Secrets are masked on read and setting
/// values are never written to logs (only the key is).
/// </summary>
public static class SystemConfigEndpoints
{
    private static readonly Action<ILogger, string, Exception?> _settingUpdated =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, "SettingUpdated"),
            "Setting '{Key}' updated by administrator.");

    public static IEndpointRouteBuilder MapSystemConfigEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/settings")
            .RequireAuthorization("RequireAdmin");

        group.MapGet("", async (SystemConfigService settings, CancellationToken ct) =>
        {
            var views = await settings.GetAllAsync(ct);
            return Results.Ok(views);
        });

        group.MapPut("/{key}", async (
            string key,
            UpdateSettingRequest request,
            SystemConfigService settings,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var result = await settings.SetAsync(key, request.Value, ct);
            if (!result.Succeeded)
            {
                return Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status400BadRequest);
            }

            // The value is never logged; only the key, so a secret can never
            // leak through request logging.
            var logger = loggerFactory.CreateLogger("OpenDockify.Api.SystemConfigEndpoints");
            _settingUpdated(logger, key, null);
            return Results.Ok(new { key, updated = true });
        });

        return endpoints;
    }
}

public sealed record UpdateSettingRequest(string Value);
