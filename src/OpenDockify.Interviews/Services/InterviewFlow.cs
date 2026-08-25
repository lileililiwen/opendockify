using System.Globalization;
using System.Text.RegularExpressions;
using OpenDockify.Templates.Models;

namespace OpenDockify.Interviews.Services;

public static class InterviewFlow
{
    public static IReadOnlyList<InterviewStepDefinition> VisibleSteps(
        InterviewDefinition interview,
        IReadOnlyDictionary<string, string> answers)
    {
        var byId = interview.Steps.ToDictionary(step => step.Id, StringComparer.Ordinal);
        var visible = new List<InterviewStepDefinition>();
        string? current = interview.StartStepId;
        while (current is not null)
        {
            var step = byId[current];
            if (Matches(step.Condition, answers))
            {
                visible.Add(step);
            }

            current = step.NextStepId;
        }

        return visible;
    }

    public static Dictionary<string, string> RemoveHiddenAnswers(
        InterviewDefinition interview,
        IReadOnlyDictionary<string, string> answers)
    {
        var visibleFields = VisibleSteps(interview, answers)
            .SelectMany(step => step.Fields)
            .ToHashSet(StringComparer.Ordinal);
        return answers
            .Where(pair => visibleFields.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    public static string? ValidateValue(FieldDefinition field, string? value)
    {
        value ??= string.Empty;
        if (field.Required && string.IsNullOrWhiteSpace(value))
        {
            return $"Field '{field.Name}' is required.";
        }

        if (value.Length == 0)
        {
            return null;
        }

        var rule = field.Validation;
        if (rule?.MinLength is int minLength && value.Length < minLength)
        {
            return $"Field '{field.Name}' must contain at least {minLength} characters.";
        }

        if (rule?.MaxLength is int maxLength && value.Length > maxLength)
        {
            return $"Field '{field.Name}' must contain at most {maxLength} characters.";
        }

        if (rule?.Pattern is string pattern)
        {
            try
            {
                if (!Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    return $"Field '{field.Name}' has an invalid format.";
                }
            }
            catch (ArgumentException)
            {
                return $"Field '{field.Name}' has an invalid validation pattern.";
            }
        }

        if (field.Type is FieldType.Number or FieldType.Currency)
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                return $"Field '{field.Name}' must be a number.";
            }

            if (rule?.NonNegative == true && number < 0)
            {
                return $"Field '{field.Name}' must not be negative.";
            }

            if (rule?.Min is decimal min && number < min)
            {
                return $"Field '{field.Name}' must be at least {min}.";
            }

            if (rule?.Max is decimal max && number > max)
            {
                return $"Field '{field.Name}' must be at most {max}.";
            }
        }

        if (field.Type == FieldType.Date
            && (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || (rule?.DateFrom is DateOnly from && date < from)
                || (rule?.DateTo is DateOnly to && date > to)))
        {
            return $"Field '{field.Name}' must be a valid date in the allowed range.";
        }

        return null;
    }

    private static bool Matches(InterviewCondition? condition, IReadOnlyDictionary<string, string> answers)
    {
        if (condition is null)
        {
            return true;
        }

        if (condition.Operator == "and")
        {
            return condition.Conditions!.All(child => Matches(child, answers));
        }

        if (condition.Operator == "or")
        {
            return condition.Conditions!.Any(child => Matches(child, answers));
        }

        answers.TryGetValue(condition.Field!, out var actual);
        return condition.Operator switch
        {
            "equals" => string.Equals(actual, condition.Value, StringComparison.Ordinal),
            "notEquals" => !string.Equals(actual, condition.Value, StringComparison.Ordinal),
            "in" => condition.Values!.Contains(actual ?? string.Empty, StringComparer.Ordinal),
            _ => false,
        };
    }
}
