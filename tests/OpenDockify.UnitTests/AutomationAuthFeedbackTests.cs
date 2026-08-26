using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using OpenDockify.Integrations.Services;
using Xunit;

namespace OpenDockify.UnitTests;

/// <summary>Structured feedback bodies for automation auth outcomes (spec: machine-readable automation feedback).</summary>
public sealed class AutomationAuthFeedbackTests
{
    [Fact]
    public async Task Challenge_writes_structured_401_with_www_authenticate()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await AutomationAuthFeedback.WriteChallengeAsync(context.Response);

        Assert.Equal(HttpStatusCode.Unauthorized, (HttpStatusCode)context.Response.StatusCode);
        Assert.Equal("ServiceToken realm=\"opendockify-automation\"", context.Response.Headers.WWWAuthenticate.ToString());
        var body = ReadJson(context.Response.Body);
        Assert.Equal("token_invalid", body.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("error").GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Forbidden_writes_structured_403_with_scope_code()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await AutomationAuthFeedback.WriteForbiddenAsync(context.Response, requiredScope: AutomationScopes.DocumentsWrite);

        Assert.Equal(HttpStatusCode.Forbidden, (HttpStatusCode)context.Response.StatusCode);
        var body = ReadJson(context.Response.Body);
        Assert.Equal("forbidden_scope", body.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains(AutomationScopes.DocumentsWrite, body.GetProperty("error").GetProperty("message").GetString());
    }

    private static JsonElement ReadJson(Stream body)
    {
        body.Position = 0;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }
}
