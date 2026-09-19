using System.Text.RegularExpressions;

namespace OpenDockify.AiAssist.Services;

/// <summary>
/// Pre-LLM PII scrub (default on via <c>Ai.ScrubPii</c>). Redacts ID numbers,
/// mainland mobile numbers, and other long digit runs with stable
/// placeholders before the prompt leaves the host. Placeholders persist in
/// the output — raw values are never re-substituted (fail-safe). The scrubbed
/// prompt is never logged; usage logs keep only the redacted 500-char
/// snippet via <see cref="AiUsageLogService"/>.
/// </summary>
public static class AiPiiScrubber
{
    private static readonly Regex _idPattern = new(
        @"(?<!\d)\d{17}[\dXx](?!\d)",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _phonePattern = new(
        @"(?<!\d)1[3-9]\d{9}(?!\d)",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _longDigitPattern = new(
        @"\d{6,}",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public static string Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var scrubbed = _idPattern.Replace(text, "[ID]");
        scrubbed = _phonePattern.Replace(scrubbed, "[PHONE]");
        scrubbed = _longDigitPattern.Replace(scrubbed, "***");
        return scrubbed;
    }

    public static bool ContainsRawPii(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return _idPattern.IsMatch(text)
            || _phonePattern.IsMatch(text);
    }
}
