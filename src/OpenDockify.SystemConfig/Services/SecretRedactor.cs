namespace OpenDockify.SystemConfig.Services;

/// <summary>
/// Secret handling for API keys and other sensitive setting values: masking
/// for admin reads and a redaction guard for log lines. The full secret is
/// never exposed by any API and must never appear in logs.
/// </summary>
public static class SecretRedactor
{
    public const string Mask = "••••";

    /// <summary>
    /// Masks a secret for display: <c>••••</c> followed by the last four
    /// characters (or the whole value when shorter).
    /// </summary>
    public static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Mask;
        }

        var last4 = value.Length > 4 ? value[^4..] : value;
        return Mask + last4;
    }

    /// <summary>
    /// Returns <paramref name="text"/> with every occurrence of
    /// <paramref name="secret"/> replaced by the mask. Use as a guard around
    /// any log line that could contain a raw secret.
    /// </summary>
    public static string Redact(string? text, string? secret)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(secret) || secret.Length < 4)
        {
            return text ?? string.Empty;
        }

        return text.Replace(secret, Mask, StringComparison.Ordinal);
    }
}
