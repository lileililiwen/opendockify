using System.Globalization;
using System.Text.RegularExpressions;
using OpenDockify.Templates.Models;

namespace OpenDockify.Generation.Services;

/// <summary>A single machine-readable field validation failure.</summary>
public sealed record DocumentFieldError(string Field, string Error);

/// <summary>
/// Shared field-level validation for document generation: required, type,
/// non-negative, range, length, and pattern rules. Used by the interactive API
/// (joined messages) and the automation API (structured errors) so both stay
/// in lockstep.
/// </summary>
public static class DocumentFieldValidation
{
    public static IReadOnlyList<DocumentFieldError> Validate(
        TemplateDefinition definition,
        GenerateCommand command)
    {
        var errors = new List<DocumentFieldError>();

        foreach (var field in definition.Fields)
        {
            var hasValue = command.Values.TryGetValue(field.Name, out var raw) && !string.IsNullOrEmpty(raw);

            if (field.Required && !hasValue)
            {
                errors.Add(new DocumentFieldError(field.Name, $"Missing required field '{field.Name}'."));
                continue;
            }

            if (!hasValue)
            {
                continue;
            }

            var fieldError = ValidateFieldValue(field, raw!);
            if (fieldError is not null)
            {
                errors.Add(new DocumentFieldError(field.Name, fieldError));
            }
        }

        return errors;
    }

    private static string? ValidateFieldValue(FieldDefinition field, string rawValue)
    {
        switch (field.Type)
        {
            case FieldType.Text:
                return ValidateTextField(field, rawValue);

            case FieldType.Number:
            case FieldType.Currency:
                return ValidateNumericField(field, rawValue);

            case FieldType.Date:
                return ValidateDateField(field, rawValue);

            default:
                return null;
        }
    }

    private static string? ValidateTextField(FieldDefinition field, string rawValue)
    {
        if (field.Validation?.MinLength is int minLen && rawValue.Length < minLen)
        {
            return $"Field '{field.Name}' must be at least {minLen} characters.";
        }

        if (field.Validation?.MaxLength is int maxLen && rawValue.Length > maxLen)
        {
            return $"Field '{field.Name}' must be at most {maxLen} characters.";
        }

        if (field.Validation?.Pattern is string pattern)
        {
            try
            {
                if (!Regex.IsMatch(rawValue, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    return $"Field '{field.Name}' does not match the required format.";
                }
            }
            catch (ArgumentException)
            {
                return $"Field '{field.Name}' has an invalid pattern rule.";
            }
        }

        return null;
    }

    private static string? ValidateNumericField(FieldDefinition field, string rawValue)
    {
        if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return $"Value '{rawValue}' for field '{field.Name}' is not a valid number.";
        }

        if (field.Type == FieldType.Currency && number < 0)
        {
            return $"Field '{field.Name}' must not be negative.";
        }

        if (field.Validation?.NonNegative == true && number < 0)
        {
            return $"Field '{field.Name}' must not be negative.";
        }

        if (field.Validation?.Min is decimal min && number < min)
        {
            return $"Field '{field.Name}' must be at least {min}.";
        }

        if (field.Validation?.Max is decimal max && number > max)
        {
            return $"Field '{field.Name}' must be at most {max}.";
        }

        return null;
    }

    private static string? ValidateDateField(FieldDefinition field, string rawValue)
    {
        if (!DateOnly.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return $"Value '{rawValue}' for field '{field.Name}' is not a valid date (expected yyyy-MM-dd).";
        }

        if (field.Validation?.DateFrom is DateOnly dateFrom && date < dateFrom)
        {
            return $"Field '{field.Name}' must be on or after {dateFrom:yyyy-MM-dd}.";
        }

        if (field.Validation?.DateTo is DateOnly dateTo && date > dateTo)
        {
            return $"Field '{field.Name}' must be on or before {dateTo:yyyy-MM-dd}.";
        }

        return null;
    }
}
