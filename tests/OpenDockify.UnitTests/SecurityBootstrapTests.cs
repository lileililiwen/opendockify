using Microsoft.Extensions.Configuration;
using OpenDockify.Auth.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class SecurityBootstrapTests
{
    [Fact]
    public void Development_does_not_apply_production_credential_rules()
    {
        var configuration = BuildConfiguration([]);

        var errors = SecurityBootstrapValidator.Validate(configuration, "Development");

        Assert.Empty(errors);
    }

    [Fact]
    public void Secure_production_configuration_is_accepted()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
            ["Seed:AdminUsername"] = "operator",
            ["Seed:AdminPassword"] = "correct-horse-battery-staple",
        });

        var errors = SecurityBootstrapValidator.Validate(configuration, "Production");

        Assert.Empty(errors);
    }

    [Fact]
    public void Production_reports_every_missing_bootstrap_credential()
    {
        var configuration = BuildConfiguration([]);

        var errors = SecurityBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, error => error.Contains("Jwt:Secret", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Seed:AdminUsername", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Seed:AdminPassword", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_unsafe_administrator_passwords()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "a-production-secret-with-at-least-32-bytes",
            ["Seed:AdminUsername"] = "operator",
            ["Seed:AdminPassword"] = "short",
        });

        var errors = SecurityBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, error => error.Contains("Seed:AdminPassword", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_unchanged_example_placeholders()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "replace-with-at-least-32-random-bytes",
            ["Seed:AdminUsername"] = "operator",
            ["Seed:AdminPassword"] = "replace-with-at-least-12-characters",
        });

        var errors = SecurityBootstrapValidator.Validate(configuration, "Production");

        Assert.Contains(errors, error => error.Contains("Jwt:Secret", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Seed:AdminPassword", StringComparison.Ordinal));
    }

    [Fact]
    public void Registration_is_disabled_by_default_and_accepts_explicit_true()
    {
        Assert.False(AuthSecurityOptions.IsRegistrationAllowed(BuildConfiguration([])));
        Assert.True(AuthSecurityOptions.IsRegistrationAllowed(BuildConfiguration(
            new Dictionary<string, string?> { ["Auth:AllowRegistration"] = "true" })));
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData("invalid", 10)]
    [InlineData("0", 10)]
    [InlineData("24", 24)]
    [InlineData("10001", 10)]
    public void Login_limit_uses_only_positive_bounded_configuration(string? configured, int expected)
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?> { ["Auth:LoginAttemptsPerMinute"] = configured });

        Assert.Equal(expected, AuthSecurityOptions.GetLoginAttemptsPerMinute(configuration));
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("invalid", 5)]
    [InlineData("-1", 5)]
    [InlineData("7", 7)]
    public void Registration_limit_uses_only_positive_bounded_configuration(string? configured, int expected)
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?> { ["Auth:RegistrationAttemptsPerHour"] = configured });

        Assert.Equal(expected, AuthSecurityOptions.GetRegistrationAttemptsPerHour(configuration));
    }

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
