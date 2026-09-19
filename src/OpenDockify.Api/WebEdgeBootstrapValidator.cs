using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Platform.Web.Cors;
using AspNetIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace OpenDockify.Api;

/// <summary>
/// String constants the edge pipeline exposes for use by other modules and
/// integration tests. Centralising the names keeps wiring and assertions in
/// lock-step.
/// </summary>
public static class WebEdgePolicies
{
    /// <summary>The platform CORS policy used for every API endpoint.</summary>
    public const string CorsPolicyName = "opendockify";
}

/// <summary>
/// Validates the configuration values the platform edge pipeline depends on.
/// Fails closed (refuses to start) when the deployer has not provided the
/// expected separation between secrets, the AI endpoint does not satisfy
/// the HTTPS posture, or a forwarded-headers allowlist would expose internal
/// addresses to client-controlled values.
/// </summary>
public static class WebEdgeBootstrapValidator
{
    private const string _loopbackOrigin = "http://localhost:8080";
    private const int _minSharingHashKeyBytes = 32;

    public static IReadOnlyList<string> Validate(IConfiguration configuration, string environmentName)
    {
        var errors = new List<string>();
        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

        var jwtSecret = configuration["Jwt:Secret"];
        var sharingHash = configuration["Sharing:HashKey"];

        if (string.IsNullOrWhiteSpace(sharingHash))
        {
            errors.Add("Sharing:HashKey is required. Set Sharing__HashKey to a secret distinct from Jwt:Secret.");
        }
        else
        {
            var bytes = System.Text.Encoding.UTF8.GetByteCount(sharingHash);
            if (bytes < _minSharingHashKeyBytes)
            {
                errors.Add($"Sharing:HashKey must be at least {_minSharingHashKeyBytes} UTF-8 bytes.");
            }

            if (!string.IsNullOrWhiteSpace(jwtSecret)
                && string.Equals(sharingHash, jwtSecret, StringComparison.Ordinal))
            {
                errors.Add("Sharing:HashKey must not equal Jwt:Secret. Set a distinct secret to keep share verifiers separate from JWT signing material.");
            }
        }

        var aiEndpoint = configuration["Ai:Endpoint"];
        if (!string.IsNullOrWhiteSpace(aiEndpoint) && !IsAiEndpointSecure(aiEndpoint, configuration, environmentName))
        {
            errors.Add("Ai:Endpoint must use https:// unless it targets loopback AND Ai__AllowInsecureHttp=true. Set Ai__Endpoint to an https:// URL or run a local Ollama instance.");
        }

        if (isProduction && !IsHstsEnabled(configuration, environmentName))
        {
            errors.Add("Web:Hsts must be enabled in Production. Set Web__Hsts=true.");
        }

        return errors;
    }

    public static void EnsureValid(IConfiguration configuration, string environmentName)
    {
        var errors = Validate(configuration, environmentName);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Unsafe web-edge configuration:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}");
        }
    }

    /// <summary>Decides whether the AI endpoint is safe to call without TLS.</summary>
    public static bool IsAiEndpointSecure(string endpoint, IConfiguration configuration, string environmentName)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        if (uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        var isLoopback = uri.IsLoopback;
        var allowInsecure = bool.TryParse(configuration["Ai:AllowInsecureHttp"], out var parsed) && parsed;
        return isLoopback && allowInsecure;
    }

    public static bool IsHstsEnabled(IConfiguration configuration, string environmentName)
    {
        if (!string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!bool.TryParse(configuration["Web:Hsts"], out var enabled))
        {
            return true;
        }
        return enabled;
    }

    public static IList<string> ResolveAllowedOrigins(IConfiguration configuration, string environmentName)
    {
        var raw = configuration["Web:Cors:AllowedOrigins"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase)
                ? new List<string>()
                : new List<string> { _loopbackOrigin };
        }

        var origins = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        return origins;
    }

    public static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IConfiguration configuration, string environmentName)
    {
        // Loopback is always trusted for local dev / self-host; production
        // must enumerate the fronting reverse proxy explicitly.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);

        var entries = ResolveForwardedHeaderEntries(configuration, environmentName);
        foreach (var entry in entries.Proxies)
        {
            if (IPAddress.TryParse(entry, out var ip))
            {
                options.KnownProxies.Add(ip);
            }
        }
        foreach (var entry in entries.Networks)
        {
            var parts = entry.Split('/', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            if (!IPAddress.TryParse(parts[0], out var prefix))
            {
                continue;
            }

            if (!int.TryParse(parts[1], out var prefixLength))
            {
                continue;
            }

            options.KnownNetworks.Add(new AspNetIpNetwork(prefix, prefixLength));
        }
    }

    /// <summary>
    /// Parses the configuration-supplied lists of trusted reverse-proxy
    /// addresses and CIDR networks into plain string lists. Production-only;
    /// development returns empty lists so the platform trusts loopback only.
    /// </summary>
    public static (IReadOnlyList<string> Proxies, IReadOnlyList<string> Networks) ResolveForwardedHeaderEntries(
        IConfiguration configuration, string environmentName)
    {
        if (!string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        var proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>();
        var networks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>();
        return (proxies, networks);
    }
}

/// <summary>
/// Helpers for resolving the client identity used in rate limiting and audit
/// keying. Prefers the forwarded client IP (when the request ran through a
/// trusted proxy) and falls back to the direct remote address.
/// </summary>
public static class WebEdgeBootstrapResolver
{
    public static string ClientPartitionKey(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
