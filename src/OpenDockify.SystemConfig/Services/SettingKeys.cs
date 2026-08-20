using System.Globalization;
using System.Text.Json;

namespace OpenDockify.SystemConfig.Services;

public enum SettingValueType
{
    Bool,
    Number,
    Date,
    Url,
    Text,
    Secret,
}

/// <summary>
/// A single allowlisted setting key: its value type, default, environment
/// variable mapping, and validation rules. The allowlist is the single source
/// of truth for the admin API's key list and for <see cref="SettingKeys"/>.
/// </summary>
public sealed record SettingKeyDefinition(
    string Key,
    SettingValueType ValueType,
    string? DefaultJson,
    string? EnvName,
    bool IsSecret = false);

/// <summary>
/// Allowlist of deployer-tunable settings. Unknown keys are rejected at the
/// API boundary so junk rows can never be inserted.
///
/// Precedence per key: environment variable (<see cref="EnvName"/>) &gt;
/// database value &gt; <see cref="DefaultJson"/>.
/// </summary>
public static class SettingKeys
{
    public const string AiEnabled = "Ai.Enabled";
    public const string AiEndpoint = "Ai.Endpoint";
    public const string AiApiKey = "Ai.ApiKey";
    public const string AiModel = "Ai.Model";
    public const string AiTimeoutSeconds = "Ai.TimeoutSeconds";
    public const string AiRateLimitPerDay = "Ai.RateLimitPerDay";
    public const string LprOneYearRate = "Lpr.OneYearRate";
    public const string LprReferenceDate = "Lpr.ReferenceDate";

    public static IReadOnlyList<SettingKeyDefinition> All { get; } = new List<SettingKeyDefinition>
    {
        new(AiEnabled, SettingValueType.Bool, "false", "AI_ENABLED"),
        new(AiEndpoint, SettingValueType.Url, "\"\"", "AI_ENDPOINT"),
        new(AiApiKey, SettingValueType.Secret, "\"\"", "AI_API_KEY", IsSecret: true),
        new(AiModel, SettingValueType.Text, "\"\"", "AI_MODEL"),
        new(AiTimeoutSeconds, SettingValueType.Number, "30", "AI_TIMEOUT_SECONDS"),
        new(AiRateLimitPerDay, SettingValueType.Number, "0", "AI_RATE_LIMIT_PER_DAY"),
        new(LprOneYearRate, SettingValueType.Number, "3.45", "LPR_ONE_YEAR_RATE"),
        new(LprReferenceDate, SettingValueType.Date, null, "LPR_REFERENCE_DATE"),
    };

    /// <summary>
    /// Effective LPR default used by the seeder; overridable by the deployer
    /// through <c>Seed:LprOneYearRate</c> (env <c>Seed__LprOneYearRate</c>).
    /// </summary>
    public const decimal DefaultLprOneYearRate = 3.45m;

    public static SettingKeyDefinition? Find(string key)
    {
        return All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// Parses and validates <paramref name="rawValue"/> against the
    /// definition's type. On success returns the JSON text to persist (a JSON
    /// scalar or quoted string); on failure returns an error message.
    /// </summary>
    public static bool TryValidate(
        SettingKeyDefinition definition,
        string? rawValue,
        out string jsonValue,
        out string? error)
    {
        jsonValue = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            error = "A value is required.";
            return false;
        }

        switch (definition.ValueType)
        {
            case SettingValueType.Bool:
                if (!bool.TryParse(rawValue.Trim(), out var boolValue))
                {
                    error = "Value must be true or false.";
                    return false;
                }

                jsonValue = boolValue ? "true" : "false";
                return true;

            case SettingValueType.Number:
                if (!decimal.TryParse(rawValue.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    error = "Value must be a number.";
                    return false;
                }

                if (decimalValue < 0 || decimalValue > 100)
                {
                    error = "Value must be between 0 and 100.";
                    return false;
                }

                jsonValue = decimalValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case SettingValueType.Date:
                if (!DateOnly.TryParse(rawValue.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                {
                    error = "Value must be a date (yyyy-MM-dd).";
                    return false;
                }

                jsonValue = JsonSerializer.Serialize(dateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                return true;

            case SettingValueType.Url:
                if (!Uri.TryCreate(rawValue.Trim(), UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    error = "Value must be an absolute http(s) URL.";
                    return false;
                }

                jsonValue = JsonSerializer.Serialize(uri.AbsoluteUri);
                return true;

            case SettingValueType.Text:
            case SettingValueType.Secret:
                if (rawValue.Length > 512)
                {
                    error = "Value is too long (max 512 characters).";
                    return false;
                }

                jsonValue = JsonSerializer.Serialize(rawValue);
                return true;

            default:
                error = "Unsupported value type.";
                return false;
        }
    }
}
