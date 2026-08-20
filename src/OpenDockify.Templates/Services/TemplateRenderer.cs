using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OpenDockify.Finance.Services;
using OpenDockify.Templates.Models;

namespace OpenDockify.Templates.Services;

public sealed record TemplateRenderResult(string? Text, string? Error)
{
    public static TemplateRenderResult Success(string text)
    {
        return new(text, null);
    }

    public static TemplateRenderResult Failure(string error)
    {
        return new(null, error);
    }
}

/// <summary>
/// Pure renderer (no DB access): replaces <c>{{field}}</c> placeholders in the
/// body and selected clause texts with filled values. Currency fields render
/// as RMB uppercase; date fields as a formatted Chinese date. A missing value
/// for a referenced field fails with an error naming the field.
/// </summary>
public static class TemplateRenderer
{
    public static TemplateRenderResult Render(
        TemplateDefinition definition,
        string body,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyCollection<string> selectedClauseIds)
    {
        var fieldsByName = definition.Fields.ToDictionary(f => f.Name, StringComparer.Ordinal);

        var unknownIds = selectedClauseIds
            .Where(id => definition.Clauses.All(c => c.Id != id))
            .ToArray();
        if (unknownIds.Length > 0)
        {
            return TemplateRenderResult.Failure($"Selected clause '{unknownIds[0]}' does not exist in the template definition.");
        }

        var bodyText = Substitute(body, fieldsByName, values);
        if (bodyText.Text is null)
        {
            return TemplateRenderResult.Failure(bodyText.Error ?? "Rendering failed.");
        }

        var sections = new List<string> { bodyText.Text };
        foreach (var clause in definition.Clauses)
        {
            if (!selectedClauseIds.Contains(clause.Id))
            {
                continue;
            }

            var clauseText = Substitute(clause.Text, fieldsByName, values);
            if (clauseText.Text is null)
            {
                return TemplateRenderResult.Failure(clauseText.Error ?? "Rendering failed.");
            }

            sections.Add(clauseText.Text);
        }

        return TemplateRenderResult.Success(string.Join("\n\n", sections));
    }

    private static (string? Text, string? Error) Substitute(
        string text,
        Dictionary<string, FieldDefinition> fieldsByName,
        IReadOnlyDictionary<string, string> values)
    {
        var sb = new StringBuilder(text.Length + 256);
        var position = 0;

        foreach (Match match in TemplateDefinitionValidator.PlaceholderPattern.Matches(text))
        {
            sb.Append(text, position, match.Index - position);

            var name = match.Groups[1].Value;
            if (!fieldsByName.TryGetValue(name, out var field))
            {
                return (null, $"Placeholder '{{{{{name}}}}}' references undeclared field '{name}'.");
            }

            if (!values.TryGetValue(name, out var rawValue) || string.IsNullOrEmpty(rawValue))
            {
                return (null, $"Missing value for field '{name}'.");
            }

            var rendered = RenderValue(field, rawValue);
            if (rendered.Text is null)
            {
                return (null, rendered.Error);
            }

            sb.Append(rendered.Text);
            position = match.Index + match.Length;
        }

        sb.Append(text, position, text.Length - position);
        return (sb.ToString(), null);
    }

    private static (string? Text, string? Error) RenderValue(FieldDefinition field, string rawValue)
    {
        switch (field.Type)
        {
            case FieldType.Text:
                return (rawValue, null);

            case FieldType.Number:
                if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                {
                    return (null, $"Value '{rawValue}' for field '{field.Name}' is not a valid number.");
                }

                return (rawValue, null);

            case FieldType.Currency:
                if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                {
                    return (null, $"Value '{rawValue}' for field '{field.Name}' is not a valid currency amount.");
                }

                var conversion = AmountToChinese.Convert(amount);
                if (!conversion.Succeeded)
                {
                    return (null, $"Field '{field.Name}': {conversion.Error}");
                }

                return (conversion.Value, null);

            case FieldType.Date:
                if (!DateOnly.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return (null, $"Value '{rawValue}' for field '{field.Name}' is not a valid date (expected yyyy-MM-dd).");
                }

                return (date.ToString("yyyy年M月d日", CultureInfo.InvariantCulture), null);

            default:
                return (null, $"Unsupported field type for '{field.Name}'.");
        }
    }
}
