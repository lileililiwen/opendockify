using Microsoft.Extensions.Configuration;
using OpenDockify.Api;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class WebEdgeBootstrapTests
{
    [Fact]
    public void Development_does_not_enforce_production_posture()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "dev-secret-please-change-for-production-use",
                ["Sharing:HashKey"] = "a-distinct-sharing-hash-with-at-least-thirty-two-bytes",
                ["Ai:Endpoint"] = "https://llm.example/v1",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Development");

        Assert.Empty(errors);
    }

    [Fact]
    public void Development_requires_sharing_hash_key()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "dev-secret-please-change-for-production-use",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Development");

        Assert.Contains(errors, e => e.Contains("Sharing:HashKey", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_missing_sharing_hash_key()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, e => e.Contains("Sharing:HashKey", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_sharing_hash_equal_to_jwt_secret()
    {
        var shared = "the-same-secret-shared-everywhere-this-should-never-pass";
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = shared,
                ["Sharing:HashKey"] = shared,
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, e => e.Contains("Sharing:HashKey", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_sharing_hash_below_minimum_length()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
                ["Sharing:HashKey"] = "too-short",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, e => e.Contains("at least 32", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_remote_http_ai_endpoint()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
                ["Sharing:HashKey"] = "a-distinct-sharing-hash-with-at-least-thirty-two-bytes",
                ["Ai:Endpoint"] = "http://remote.llm.example/v1",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, e => e.Contains("Ai:Endpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_accepts_loopback_ai_when_insecure_flag_is_set()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
                ["Sharing:HashKey"] = "a-distinct-sharing-hash-with-at-least-thirty-two-bytes",
                ["Ai:Endpoint"] = "http://localhost:11434/v1",
                ["Ai:AllowInsecureHttp"] = "true",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.DoesNotContain(errors, e => e.Contains("Ai:Endpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_loopback_ai_without_insecure_flag()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
                ["Sharing:HashKey"] = "a-distinct-sharing-hash-with-at-least-thirty-two-bytes",
                ["Ai:Endpoint"] = "http://localhost:11434/v1",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, e => e.Contains("Ai:Endpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_accepts_https_ai_endpoint()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
                ["Sharing:HashKey"] = "a-distinct-sharing-hash-with-at-least-thirty-two-bytes",
                ["Ai:Endpoint"] = "https://llm.example/v1",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.DoesNotContain(errors, e => e.Contains("Ai:Endpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_requires_hsts_when_hsts_setting_missing()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
                ["Sharing:HashKey"] = "a-distinct-sharing-hash-with-at-least-thirty-two-bytes",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.DoesNotContain(errors, e => e.Contains("Web:Hsts", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_explicit_hsts_disabled()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
                ["Sharing:HashKey"] = "a-distinct-sharing-hash-with-at-least-thirty-two-bytes",
                ["Web:Hsts"] = "false",
            });

        var errors = WebEdgeBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, e => e.Contains("Web:Hsts", StringComparison.Ordinal));
    }

    [Fact]
    public void Allowed_origins_default_to_loopback_outside_production()
    {
        var configuration = BuildConfiguration([]);

        var origins = WebEdgeBootstrapValidator.ResolveAllowedOrigins(configuration, "Development");

        Assert.Single(origins);
        Assert.Equal("http://localhost:8080", origins[0]);
    }

    [Fact]
    public void Allowed_origins_default_to_empty_in_production()
    {
        var configuration = BuildConfiguration([]);

        var origins = WebEdgeBootstrapValidator.ResolveAllowedOrigins(configuration, "Production");

        Assert.Empty(origins);
    }

    [Fact]
    public void Allowed_origins_use_explicit_configuration_when_set()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Web:Cors:AllowedOrigins"] = "https://app.example,https://admin.example",
            });

        var origins = WebEdgeBootstrapValidator.ResolveAllowedOrigins(configuration, "Production");

        Assert.Equal(2, origins.Count);
        Assert.Contains("https://app.example", origins);
        Assert.Contains("https://admin.example", origins);
    }

    [Fact]
    public void Forwarded_headers_trust_only_loopback_outside_production()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "203.0.113.1",
                ["ForwardedHeaders:KnownNetworks:0"] = "203.0.113.0/24",
            });

        var entries = WebEdgeBootstrapValidator.ResolveForwardedHeaderEntries(configuration, "Development");

        Assert.Empty(entries.Proxies);
        Assert.Empty(entries.Networks);
    }

    [Fact]
    public void Forwarded_headers_apply_configured_proxies_in_production()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "203.0.113.1",
                ["ForwardedHeaders:KnownProxies:1"] = "203.0.113.2",
                ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8",
            });

        var entries = WebEdgeBootstrapValidator.ResolveForwardedHeaderEntries(configuration, "Production");

        Assert.Equal(2, entries.Proxies.Count);
        Assert.Contains("203.0.113.1", entries.Proxies);
        Assert.Contains("203.0.113.2", entries.Proxies);
        Assert.Single(entries.Networks);
        Assert.Contains("10.0.0.0/8", entries.Networks);
    }

    [Fact]
    public void Ensure_valid_throws_for_unsafe_production_configuration()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
            });

        var ex = Assert.Throws<InvalidOperationException>(
            () => WebEdgeBootstrapValidator.EnsureValid(configuration, "Production"));

        Assert.Contains("Sharing:HashKey", ex.Message);
    }

    [Fact]
    public void Ai_endpoint_security_only_accepts_https_or_loopback_with_flag()
    {
        var httpsConfig = BuildConfiguration([]);
        Assert.True(WebEdgeBootstrapValidator.IsAiEndpointSecure(
            "https://llm.example", httpsConfig, "Production"));

        var httpRemoteConfig = BuildConfiguration([]);
        Assert.False(WebEdgeBootstrapValidator.IsAiEndpointSecure(
            "http://llm.example", httpRemoteConfig, "Production"));

        var httpLoopbackFlagConfig = BuildConfiguration(
            new Dictionary<string, string?> { ["Ai:AllowInsecureHttp"] = "true" });
        Assert.True(WebEdgeBootstrapValidator.IsAiEndpointSecure(
            "http://localhost:11434", httpLoopbackFlagConfig, "Production"));

        var httpLoopbackNoFlagConfig = BuildConfiguration([]);
        Assert.False(WebEdgeBootstrapValidator.IsAiEndpointSecure(
            "http://localhost:11434", httpLoopbackNoFlagConfig, "Production"));

        var garbage = BuildConfiguration([]);
        Assert.False(WebEdgeBootstrapValidator.IsAiEndpointSecure(
            "not a url", garbage, "Production"));
    }

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
