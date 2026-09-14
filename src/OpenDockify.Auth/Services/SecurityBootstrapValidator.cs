using System.Text;
using Microsoft.Extensions.Configuration;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Validates the credentials that make a production deployment safe to start.
/// Development keeps its explicit local-only defaults for a zero-friction loop.
/// </summary>
public static class SecurityBootstrapValidator
{
    public const int MinAdminPasswordLength = 12;
    public static IReadOnlyList<string> Validate(IConfiguration configuration, string environmentName)
    {
        if (!string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var errors = new List<string>();
        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret)
            || Encoding.UTF8.GetByteCount(jwtSecret) < JwtTokenService.MinSecretBytes
            || IsExamplePlaceholder(jwtSecret))
        {
            errors.Add($"Jwt:Secret is required in Production and must be at least {JwtTokenService.MinSecretBytes} UTF-8 bytes. Set Jwt__Secret.");
        }

        if (string.IsNullOrWhiteSpace(configuration["Seed:AdminUsername"]))
        {
            errors.Add("Seed:AdminUsername is required in Production. Set Seed__AdminUsername.");
        }

        var adminPassword = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword)
            || adminPassword.Length < MinAdminPasswordLength
            || IsExamplePlaceholder(adminPassword))
        {
            errors.Add($"Seed:AdminPassword is required in Production, must be at least {MinAdminPasswordLength} characters, and must not use the development password. Set Seed__AdminPassword.");
        }

        return errors;
    }

    public static void EnsureValid(IConfiguration configuration, string environmentName)
    {
        var errors = Validate(configuration, environmentName);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Unsafe production configuration:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}");
        }
    }

    private static bool IsExamplePlaceholder(string value)
    {
        return value.StartsWith("replace-with-", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Typed access to authentication exposure and abuse limits.</summary>
public static class AuthSecurityOptions
{
    public const string LoginPolicyName = "auth-login";
    public const string RegistrationPolicyName = "auth-registration";
    public const string RecoveryPolicyName = "auth-recovery";
    public const int DefaultLoginAttemptsPerMinute = 10;
    public const int DefaultRegistrationAttemptsPerHour = 5;
    public const int DefaultRecoveryAttemptsPerHour = 3;
    private const int _maxAttempts = 10_000;

    public static bool IsRegistrationAllowed(IConfiguration configuration)
    {
        return bool.TryParse(configuration["Auth:AllowRegistration"], out var enabled) && enabled;
    }

    public static int GetLoginAttemptsPerMinute(IConfiguration configuration)
    {
        return GetPositiveBoundedInt(configuration["Auth:LoginAttemptsPerMinute"], DefaultLoginAttemptsPerMinute);
    }

    public static int GetRegistrationAttemptsPerHour(IConfiguration configuration)
    {
        return GetPositiveBoundedInt(configuration["Auth:RegistrationAttemptsPerHour"], DefaultRegistrationAttemptsPerHour);
    }

    public static int GetRecoveryAttemptsPerHour(IConfiguration configuration)
    {
        return GetPositiveBoundedInt(configuration["Auth:RecoveryAttemptsPerHour"], DefaultRecoveryAttemptsPerHour);
    }

    private static int GetPositiveBoundedInt(string? raw, int fallback)
    {
        return int.TryParse(raw, out var value) && value > 0 && value <= _maxAttempts
            ? value
            : fallback;
    }
}
