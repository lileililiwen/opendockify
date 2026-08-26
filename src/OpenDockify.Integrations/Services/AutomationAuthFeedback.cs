using Microsoft.AspNetCore.Http;

namespace OpenDockify.Integrations.Services;

/// <summary>
/// Structured feedback bodies for automation auth outcomes so 401/403 match the
/// `{error:{code,message}}` contract used by every other automation error.
/// </summary>
public static class AutomationAuthFeedback
{
    public static async Task WriteChallengeAsync(HttpResponse response)
    {
        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.Headers.WWWAuthenticate = $"{AutomationAuthorization.SchemeName} realm=\"opendockify-automation\"";
        await WriteAsync(response, "token_invalid",
            "A valid service token is required. Send 'Authorization: Bearer odk_…'.");
    }

    public static async Task WriteForbiddenAsync(HttpResponse response, string requiredScope)
    {
        response.StatusCode = StatusCodes.Status403Forbidden;
        await WriteAsync(response, "forbidden_scope",
            $"The service token lacks the required scope '{requiredScope}'.");
    }

    private static async Task WriteAsync(HttpResponse response, string code, string message)
    {
        if (response.HasStarted)
        {
            return;
        }

        // Serialize explicitly so the error shape matches the documented
        // contract byte-for-byte.
        response.ContentType = "application/json; charset=utf-8";
        var json = System.Text.Json.JsonSerializer.Serialize(
            new AutomationError(new AutomationErrorDetail(code, message, null)),
            _jsonOptions);
        await response.WriteAsync(json);
    }

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
}
