using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenDockify.Auth.Services;
using OpenDockify.Finance.Services;
using OpenDockify.Generation.Models;
using OpenDockify.Rendering.Services;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;

namespace OpenDockify.Generation.Services;

public enum GenerationErrorKind
{
    None,
    NotFound,
    Forbidden,
    Validation,
    RenderError,
}

public sealed record GenerateResult(
    Document? Document,
    IReadOnlyList<string> Warnings,
    GenerationErrorKind ErrorKind,
    string? Error)
{
    public static GenerateResult Success(Document document, IReadOnlyList<string> warnings)
    {
        return new(document, warnings, GenerationErrorKind.None, null);
    }

    public static GenerateResult Failure(GenerationErrorKind kind, string error)
    {
        return new(null, [], kind, error);
    }
}

public sealed record GenerateCommand(
    Guid TemplateId,
    Dictionary<string, string> Values,
    List<string> SelectedClauseIds);

public sealed record DocumentListPage(
    IReadOnlyList<Document> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>Parameter snapshot stored on each document record.</summary>
public sealed record DocumentSnapshot(
    Dictionary<string, string> Values,
    List<string> SelectedClauseIds);

/// <summary>
/// Orchestrates document generation: load accessible template → validate values
/// (required, type, non-negative, range — blocking) → collect finance warnings
/// (interest-rate vs LPR, non-blocking) → render → append risk notice → PDF →
/// persist immutable record. Re-edit creates a new record with
/// <see cref="Document.ParentId"/> set; the original never changes.
/// </summary>
public sealed class DocumentService(
    DbContext db,
    TemplateService templateService,
    IPdfRenderer pdfRenderer,
    InterestRateService interestRateService,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GenerateResult> GenerateAsync(
        Guid userId,
        GenerateCommand command,
        CancellationToken cancellationToken = default)
    {
        var templateResult = await templateService.GetByIdAsync(userId, command.TemplateId, cancellationToken);
        if (templateResult.NotFound)
        {
            return GenerateResult.Failure(GenerationErrorKind.NotFound, "Template not found.");
        }

        var template = templateResult.Value!;
        var definitionValidation = TemplateDefinitionValidator.Validate(template.DefinitionJson, template.Body);
        if (definitionValidation.Error is not null)
        {
            return GenerateResult.Failure(GenerationErrorKind.Validation, definitionValidation.Error);
        }

        var definition = definitionValidation.Definition!;
        var validationErrors = ValidateValues(definition, command);
        var warnings = new List<string>();

        if (validationErrors.Count > 0)
        {
            return GenerateResult.Failure(GenerationErrorKind.Validation, string.Join(" ", validationErrors));
        }

        await CollectInterestWarningsAsync(definition, command.Values, warnings, cancellationToken);

        var render = TemplateRenderer.Render(definition, template.Body, command.Values, command.SelectedClauseIds);
        if (render.Text is null)
        {
            return GenerateResult.Failure(GenerationErrorKind.Validation, render.Error ?? "Rendering failed.");
        }

        var fullText = AppendRiskNotice(render.Text, template.RiskNoticeText);
        var document = new Document
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            TemplateId = template.Id,
            Status = DocumentStatus.Generated,
            SnapshotJson = SerializeSnapshot(command),
            RenderedText = fullText,
            CreatedAt = DateTime.UtcNow,
        };

        var documentsPath = configuration["Storage:DocumentsPath"] ?? "/app/data/documents";
        document.PdfPath = Path.Combine(documentsPath, userId.ToString(), $"{document.Id}.pdf");

        try
        {
            await pdfRenderer.RenderAsync(fullText, document.PdfPath, cancellationToken);
        }
        catch (Exception ex)
        {
            return GenerateResult.Failure(GenerationErrorKind.RenderError, $"PDF rendering failed: {ex.Message}");
        }

        db.Set<Document>().Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return GenerateResult.Success(document, warnings);
    }

    /// <summary>
    /// Re-edits a document: creates a NEW record with the same template and
    /// the supplied (changed) values, linking <c>parentId</c> to the original.
    /// The original record is never modified.
    /// </summary>
    public async Task<GenerateResult> ReEditAsync(
        Guid userId,
        Guid documentId,
        GenerateCommand command,
        CancellationToken cancellationToken = default)
    {
        var original = await db.Set<Document>()
            .SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (original is null || original.OwnerId != userId)
        {
            return GenerateResult.Failure(GenerationErrorKind.NotFound, "Document not found.");
        }

        var result = await GenerateAsync(userId, command, cancellationToken);
        if (result.Document is not null)
        {
            result.Document.ParentId = original.Id;
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public async Task<DocumentListPage> ListAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Set<Document>()
            .Where(d => d.OwnerId == userId)
            .OrderByDescending(d => d.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new DocumentListPage(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<OwnedResourceResult<Document>> GetAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await db.Set<Document>()
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document is null || document.OwnerId != userId)
        {
            return OwnedResourceResult.Missing<Document>();
        }

        return OwnedResourceResult.Found(document);
    }

    public async Task<OwnedResourceResult<Document>> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await db.Set<Document>()
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document is null || document.OwnerId != userId)
        {
            return OwnedResourceResult.Missing<Document>();
        }

        db.Set<Document>().Remove(document);
        await db.SaveChangesAsync(cancellationToken);

        if (File.Exists(document.PdfPath))
        {
            File.Delete(document.PdfPath);
        }

        return OwnedResourceResult.Found(document);
    }

    private static string AppendRiskNotice(string renderedText, string riskNotice)
    {
        if (string.IsNullOrWhiteSpace(riskNotice))
        {
            return renderedText;
        }

        return $"{renderedText}\n\n{riskNotice}";
    }

    private static string SerializeSnapshot(GenerateCommand command)
    {
        var snapshot = new DocumentSnapshot(command.Values, command.SelectedClauseIds);
        return JsonSerializer.Serialize(snapshot, _jsonOptions);
    }

    private static List<string> ValidateValues(TemplateDefinition definition, GenerateCommand command)
    {
        var errors = new List<string>();

        foreach (var field in definition.Fields)
        {
            var hasValue = command.Values.TryGetValue(field.Name, out var raw) && !string.IsNullOrEmpty(raw);

            if (field.Required && !hasValue)
            {
                errors.Add($"Missing required field '{field.Name}'.");
                continue;
            }

            if (!hasValue)
            {
                continue;
            }

            var fieldError = ValidateFieldValue(field, raw!);
            if (fieldError is not null)
            {
                errors.Add(fieldError);
            }
        }

        return errors;
    }

    private async Task CollectInterestWarningsAsync(
        TemplateDefinition definition,
        Dictionary<string, string> values,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var field in definition.Fields)
        {
            if (field.Validation?.IsInterestRate != true)
            {
                continue;
            }

            if (!values.TryGetValue(field.Name, out var raw) || !decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                continue;
            }

            var validation = await interestRateService.ValidateAsync(rate, cancellationToken);
            if (validation.Level != RateLevel.Ok)
            {
                warnings.Add(validation.Message);
            }
        }
    }

    private static string? ValidateFieldValue(FieldDefinition field, string rawValue)
    {
        switch (field.Type)
        {
            case FieldType.Text:
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

            case FieldType.Number:
            case FieldType.Currency:
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

            case FieldType.Date:
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

            default:
                return null;
        }
    }
}
