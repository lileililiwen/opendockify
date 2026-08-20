namespace OpenDockify.Templates.Models;

public enum FieldType
{
    Text,
    Number,
    Currency,
    Date,
}

/// <summary>
/// Typed model of a template's JSON definition: the contract for form fields
/// and optional clauses. The raw JSON is stored as <c>Template.DefinitionJson</c>;
/// this model is the source of truth for validation and rendering.
/// </summary>
public sealed class TemplateDefinition
{
    public List<FieldDefinition> Fields { get; set; } = [];

    public List<ClauseDefinition> Clauses { get; set; } = [];
}

public sealed class FieldDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public FieldType Type { get; set; }

    public bool Required { get; set; }

    public ValidationRule? Validation { get; set; }
}

public sealed class ClauseDefinition
{
    /// <summary>Unique within a definition; used to select clauses on render.</summary>
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Optional per-field validation rules. Applied by the validator at save time
/// and (for value-level checks) by the generation flow later.
/// </summary>
public sealed class ValidationRule
{
    public bool NonNegative { get; set; }

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public int? MinLength { get; set; }

    public int? MaxLength { get; set; }

    public string? Pattern { get; set; }

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    /// <summary>
    /// Marks a number field as an annual interest rate (percent): the
    /// generation flow compares its value against the configured LPR and
    /// returns a non-blocking warning when it exceeds the LPR or 4× LPR.
    /// </summary>
    public bool IsInterestRate { get; set; }
}
