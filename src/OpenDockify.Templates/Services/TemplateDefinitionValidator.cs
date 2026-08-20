using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OpenDockify.Templates.Models;

namespace OpenDockify.Templates.Services;

public enum DefinitionErrorKind
{
    None,
    InvalidJson,
    TooLarge,
    Invalid,
}

public sealed record TemplateDefinitionValidation(
    bool IsValid,
    string? Error,
    TemplateDefinition? Definition,
    DefinitionErrorKind Kind = DefinitionErrorKind.None)
{
    public static TemplateDefinitionValidation Success(TemplateDefinition definition)
    {
        return new(true, null, definition);
    }

    public static TemplateDefinitionValidation Failure(DefinitionErrorKind kind, string error)
    {
        return new(false, error, null, kind);
    }
}

/// <summary>
/// Parses and validates a template's JSON definition at save time: known field
/// types, unique clause ids, placeholder→field consistency, and size/depth
/// caps. Unknown JSON properties are rejected so a malicious or typo'd payload
/// cannot silently drop fields.
/// </summary>
public static class TemplateDefinitionValidator
{
    public const int MaxDefinitionChars = 100_000;
    public const int MaxDepth = 32;
    public const int MaxFields = 200;
    public const int MaxClauses = 50;

    /// <summary>Matches <c>{{variable}}</c> placeholders; names are [A-Za-z0-9_].</summary>
    public static readonly Regex PlaceholderPattern = new(
        @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _fieldNamePattern = new(
        @"^[A-Za-z0-9_]+$",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    /// <summary>
    /// Strict JSON options shared by parse (validator), re-serialization
    /// (copy/admin), and the renderer tests.
    /// </summary>
    public static JsonSerializerOptions JsonOptions => _jsonOptions;

    public static TemplateDefinitionValidation Validate(string definitionJson, string body)
    {
        if (definitionJson.Length > MaxDefinitionChars)
        {
            return TemplateDefinitionValidation.Failure(
                DefinitionErrorKind.TooLarge,
                $"Definition exceeds the maximum size of {MaxDefinitionChars} characters.");
        }

        TemplateDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<TemplateDefinition>(definitionJson, _jsonOptions)
                ?? throw new JsonException("Definition is empty.");
        }
        catch (JsonException ex)
        {
            return TemplateDefinitionValidation.Failure(
                DefinitionErrorKind.InvalidJson,
                $"Definition is not valid JSON: {ex.Message}");
        }

        if (definition.Fields.Count > MaxFields)
        {
            return TemplateDefinitionValidation.Failure(
                DefinitionErrorKind.Invalid,
                $"Definition exceeds the maximum of {MaxFields} fields.");
        }

        if (definition.Clauses.Count > MaxClauses)
        {
            return TemplateDefinitionValidation.Failure(
                DefinitionErrorKind.Invalid,
                $"Definition exceeds the maximum of {MaxClauses} clauses.");
        }

        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in definition.Fields.Select(f => f.Name))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return TemplateDefinitionValidation.Failure(DefinitionErrorKind.Invalid, "Every field must have a name.");
            }

            if (!_fieldNamePattern.IsMatch(name))
            {
                return TemplateDefinitionValidation.Failure(
                    DefinitionErrorKind.Invalid,
                    $"Field name '{name}' may only contain letters, digits, and underscores.");
            }

            if (!fieldNames.Add(name))
            {
                return TemplateDefinitionValidation.Failure(
                    DefinitionErrorKind.Invalid,
                    $"Duplicate field name '{name}'.");
            }
        }

        var clauseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in definition.Clauses.Select(c => c.Id))
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return TemplateDefinitionValidation.Failure(DefinitionErrorKind.Invalid, "Every clause must have an id.");
            }

            if (!clauseIds.Add(id))
            {
                return TemplateDefinitionValidation.Failure(
                    DefinitionErrorKind.Invalid,
                    $"Duplicate clause id '{id}'.");
            }
        }

        var placeholderCheck = CheckPlaceholders(definition, body);
        if (placeholderCheck is not null)
        {
            return TemplateDefinitionValidation.Failure(DefinitionErrorKind.Invalid, placeholderCheck);
        }

        return TemplateDefinitionValidation.Success(definition);
    }

    private static string? CheckPlaceholders(TemplateDefinition definition, string body)
    {
        var allText = new System.Text.StringBuilder(body.Length + 1024);
        allText.Append(body);
        foreach (var clause in definition.Clauses)
        {
            allText.Append('\n');
            allText.Append(clause.Text);
        }

        foreach (Match match in PlaceholderPattern.Matches(allText.ToString()))
        {
            var name = match.Groups[1].Value;
            if (!definition.Fields.Any(f => f.Name == name))
            {
                return $"Placeholder '{{{{{name}}}}}' references undeclared field '{name}'.";
            }
        }

        return null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            MaxDepth = MaxDepth,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new FieldTypeJsonConverter());
        return options;
    }
}

/// <summary>
/// Serializes <see cref="FieldType"/> using the documented JSON contract names
/// (<c>string</c>, <c>number</c>, <c>currency</c>, <c>date</c>) while the C#
/// enum member is <see cref="FieldType.Text"/> (CA1720 forbids the
/// type-name member <c>String</c>).
/// </summary>
public sealed class FieldTypeJsonConverter : JsonConverter<FieldType>
{
    private static readonly Dictionary<string, FieldType> _byName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = FieldType.Text,
        ["number"] = FieldType.Number,
        ["currency"] = FieldType.Currency,
        ["date"] = FieldType.Date,
    };

    private static readonly Dictionary<FieldType, string> _byValue =
        _byName.ToDictionary(kv => kv.Value, kv => kv.Key);

    public override FieldType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (raw is not null && _byName.TryGetValue(raw, out var fieldType))
        {
            return fieldType;
        }

        throw new JsonException(
            $"Unknown field type '{raw}'. Supported types: string, number, currency, date.");
    }

    public override void Write(Utf8JsonWriter writer, FieldType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(_byValue[value]);
    }
}
