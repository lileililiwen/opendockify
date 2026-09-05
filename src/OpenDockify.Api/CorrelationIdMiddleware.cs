using Microsoft.Extensions.Logging;

namespace OpenDockify.Api;

/// Adds a stable request correlation id to responses and every log scope for
/// the request. Client-provided values are bounded and printable.
public sealed class CorrelationIdMiddleware
{
    private const string _headerName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[_headerName].ToString();
        var correlationId = IsSafe(supplied) ? supplied : Guid.NewGuid().ToString("N");
        context.Response.Headers[_headerName] = correlationId;
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            [_headerName] = correlationId,
        });
        await _next(context);
    }

    private static bool IsSafe(string value)
    {
        return value.Length is > 0 and <= 64
            && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
    }
}
