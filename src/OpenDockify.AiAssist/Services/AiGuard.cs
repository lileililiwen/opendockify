using System.Text;
using System.Text.RegularExpressions;

namespace OpenDockify.AiAssist.Services;

/// <summary>
/// Mechanical anti-fabrication guard — not prompt reliance:
/// <list type="bullet">
/// <item><see cref="StripFabricated"/> removes numeric/ID patterns the LLM
/// invented that were not present in the input.</item>
/// <item><see cref="TokenizeValues"/> swaps exact rendered field values for
/// sentinel tokens before the LLM call, and
/// <see cref="RestoreMandatoryValues"/> swaps them back verbatim afterwards —
/// so a mandatory value can never drift. Returns <c>null</c> when the LLM
/// dropped a mandatory token (callers fail safe to the original text).</item>
/// <item><see cref="RemoveUnselectedClauseTitles"/> strips titles of clauses
/// that were not selected.</item>
/// </list>
/// </summary>
public static class AiGuard
{
    private const string _tokenOpen = "⟦";
    private const string _tokenClose = "⟧";

    private static readonly Regex _numberOrIdPattern = new(
        @"\d{17}[\dXx]|\d+(\.\d+)?",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public static string Token(string fieldName)
    {
        return $"{_tokenOpen}{fieldName}{_tokenClose}";
    }

    /// <summary>
    /// Replaces each rendered field value in <paramref name="text"/> with its
    /// sentinel token so the LLM never sees (and cannot alter) the raw value.
    /// </summary>
    public static string TokenizeValues(string text, IReadOnlyDictionary<string, string> renderedValues)
    {
        var result = text;
        foreach (var (name, value) in renderedValues.OrderByDescending(kv => kv.Value.Length))
        {
            if (!string.IsNullOrEmpty(value))
            {
                result = result.Replace(value, Token(name), StringComparison.Ordinal);
            }
        }

        return result;
    }

    /// <summary>
    /// Replaces sentinel tokens back with the exact original values. Returns
    /// <c>null</c> if any mandatory token is missing from the LLM output, so
    /// the caller can fail safe to the original text.
    /// </summary>
    public static string? RestoreMandatoryValues(string text, IReadOnlyDictionary<string, string> renderedValues)
    {
        var result = text;
        foreach (var (name, value) in renderedValues)
        {
            var token = Token(name);
            if (!result.Contains(token, StringComparison.Ordinal))
            {
                return null;
            }

            result = result.Replace(token, value, StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// Removes numeric/ID patterns in <paramref name="text"/> that do not
    /// appear in <paramref name="original"/> (the LLM fabricated them),
    /// replacing each with <c>***</c>.
    /// </summary>
    public static string StripFabricated(string original, string text)
    {
        var replacements = _numberOrIdPattern
            .Matches(text)
            .OfType<Match>()
            .Where(m => !original.Contains(m.Value, StringComparison.Ordinal))
            .Select(m => (m.Index, m.Length))
            .ToList();

        if (replacements.Count == 0)
        {
            return text;
        }

        var sb = new StringBuilder(text);
        foreach (var (start, length) in replacements.OrderByDescending(r => r.Index))
        {
            sb.Remove(start, length);
            sb.Insert(start, "***");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Removes any occurrence of the given (unselected) clause titles from the
    /// polished text so unselected clauses cannot be introduced.
    /// </summary>
    public static string RemoveUnselectedClauseTitles(string text, IReadOnlyCollection<string> titles)
    {
        var result = text;
        foreach (var title in titles.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            result = result.Replace(title, string.Empty, StringComparison.Ordinal);
        }

        return result;
    }
}
