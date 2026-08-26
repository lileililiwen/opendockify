using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Generation.Models;
using OpenDockify.Generation.Services;
using OpenDockify.Integrations.Models;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;

namespace OpenDockify.Integrations.Services;

public sealed record AutomationTemplateItem(
    Guid Id,
    string Name,
    string Category,
    string Description,
    int CurrentRevision);

public sealed record AutomationPreviewResult(
    string? RenderedText,
    string? TemplateName,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<DocumentFieldError>? FieldErrors,
    GenerationErrorKind ErrorKind,
    string? Error);

/// <summary>Stable success body of the finalize operation (also the webhook data payload).</summary>
public sealed record AutomationDocumentPayload(
    Guid DocumentId,
    Guid TemplateId,
    string TemplateName,
    string Title,
    string? ContentSha256,
    IReadOnlyList<string> Warnings,
    DateTime CreatedAtUtc);

public sealed record AutomationErrorDetail(
    string Code,
    string Message,
    IReadOnlyList<DocumentFieldError>? Fields);

public sealed record AutomationError(AutomationErrorDetail Error);

public enum AutomationFinalizeKind
{
    Success,
    Replay,
    Conflict,
    Validation,
    NotFound,
    Forbidden,
    RenderError,
}

public sealed record AutomationFinalizeResult(
    AutomationFinalizeKind Kind,
    int StatusCode,
    string ResponseJson,
    Guid? DocumentId);

public sealed record AutomationOperationView(
    Guid OperationId,
    string Route,
    string Outcome,
    int StatusCode,
    Guid? DocumentId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

/// <summary>
/// Automation API orchestration: reuses the existing template and generation
/// services (no duplicated domain behavior), wraps mutations in transactional
/// idempotency, and records a transactional outbox event for webhook fan-out.
/// </summary>
public sealed class AutomationService(
    DbContext db,
    TemplateService templateService,
    DocumentService documentService,
    IdempotencyService idempotencyService)
{
    public static readonly string FinalizeRoute = "/api/v1/automation/finalize";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AutomationTemplateItem>> ListTemplatesAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var templates = await templateService.GetMarketplaceAsync(ownerId, cancellationToken);
        return [.. templates
            .OrderBy(t => t.Name)
            .Select(t => new AutomationTemplateItem(t.Id, t.Name, t.Category, t.Description, t.CurrentRevision))];
    }

    public async Task<AutomationPreviewResult> PreviewAsync(
        Guid ownerId,
        GenerateCommand command,
        CancellationToken cancellationToken = default)
    {
        var prevalidation = await PreValidateAsync(ownerId, command, cancellationToken);
        if (prevalidation.ErrorKind != GenerationErrorKind.None)
        {
            return new AutomationPreviewResult(
                null, null, [], prevalidation.FieldErrors, prevalidation.ErrorKind, prevalidation.Error);
        }

        var preview = await documentService.PreviewAsync(ownerId, command, cancellationToken);
        return preview.ErrorKind == GenerationErrorKind.None
            ? new AutomationPreviewResult(
                preview.RenderedText, preview.TemplateName, preview.Warnings, null, GenerationErrorKind.None, null)
            : new AutomationPreviewResult(
                null, null, [], null, preview.ErrorKind, preview.Error);
    }

    public async Task<AutomationFinalizeResult> FinalizeAsync(
        Guid ownerId,
        Guid tokenId,
        string key,
        string route,
        string requestDigest,
        GenerateCommand command,
        CancellationToken cancellationToken = default)
    {
        var prevalidation = await PreValidateAsync(ownerId, command, cancellationToken);
        if (prevalidation.ErrorKind != GenerationErrorKind.None)
        {
            var statusCode = prevalidation.ErrorKind == GenerationErrorKind.NotFound ? 404 : 422;
            return new AutomationFinalizeResult(
                prevalidation.ErrorKind == GenerationErrorKind.NotFound
                    ? AutomationFinalizeKind.NotFound
                    : AutomationFinalizeKind.Validation,
                statusCode,
                SerializeError(prevalidation.ErrorCode!, prevalidation.Error!, prevalidation.FieldErrors),
                null);
        }

        var execution = await idempotencyService.ExecuteAsync(
            new IdempotencyRequest(tokenId, ownerId, key, route, requestDigest),
            async ct =>
            {
                var generated = await documentService.GenerateAsync(ownerId, command, ct);
                if (generated.ErrorKind != GenerationErrorKind.None)
                {
                    return ToFailedOutcome(generated);
                }

                var payload = new AutomationDocumentPayload(
                    generated.Document!.Id,
                    command.TemplateId,
                    generated.TemplateName ?? string.Empty,
                    generated.Document.Title,
                    generated.Document.ContentSha256,
                    generated.Warnings,
                    generated.Document.CreatedAt);
                RecordOutboxEvent(ownerId, payload);
                return new MutationOutcome(201, payload, generated.Document.Id, Succeeded: true);
            },
            cancellationToken);

        if (execution.Conflict)
        {
            return new AutomationFinalizeResult(
                AutomationFinalizeKind.Conflict, execution.StatusCode, execution.ResponseJson, null);
        }

        if (execution.Replayed)
        {
            return new AutomationFinalizeResult(
                AutomationFinalizeKind.Replay, execution.StatusCode, execution.ResponseJson, null);
        }

        var kind = execution.StatusCode switch
        {
            201 => AutomationFinalizeKind.Success,
            403 => AutomationFinalizeKind.Forbidden,
            404 => AutomationFinalizeKind.NotFound,
            422 => AutomationFinalizeKind.Validation,
            _ => AutomationFinalizeKind.RenderError,
        };
        return new AutomationFinalizeResult(kind, execution.StatusCode, execution.ResponseJson, execution.DocumentId);
    }

    public async Task<AutomationOperationView?> GetOperationAsync(
        Guid ownerId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var record = await db.Set<IdempotencyRecord>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == operationId && x.OwnerId == ownerId, cancellationToken);
        return record is null
            ? null
            : new AutomationOperationView(
                record.Id,
                record.Route,
                record.Outcome.ToString().ToLowerInvariant(),
                record.StatusCode,
                record.DocumentId,
                record.CreatedAtUtc,
                record.ExpiresAtUtc);
    }

    private async Task<PreValidation> PreValidateAsync(
        Guid ownerId,
        GenerateCommand command,
        CancellationToken cancellationToken)
    {
        var templateResult = await templateService.GetByIdAsync(ownerId, command.TemplateId, cancellationToken);
        if (templateResult.NotFound)
        {
            return new PreValidation(
                GenerationErrorKind.NotFound, "not_found", "Template not found.", null);
        }

        var template = templateResult.Value!;
        var definitionValidation = TemplateDefinitionValidator.Validate(template.DefinitionJson, template.Body);
        if (definitionValidation.Error is not null)
        {
            return new PreValidation(
                GenerationErrorKind.Validation, "template_invalid", definitionValidation.Error, null);
        }

        var fieldErrors = DocumentFieldValidation.Validate(definitionValidation.Definition!, command);
        if (fieldErrors.Count > 0)
        {
            return new PreValidation(
                GenerationErrorKind.Validation,
                "validation_failed",
                "One or more fields are invalid.",
                fieldErrors);
        }

        return new PreValidation(GenerationErrorKind.None, null, null, null);
    }

    private void RecordOutboxEvent(Guid ownerId, AutomationDocumentPayload payload)
    {
        var eventId = Guid.NewGuid();
        var envelope = new
        {
            eventId,
            type = AutomationEvents.DocumentFinalizedName,
            occurredAtUtc = DateTime.UtcNow,
            ownerId,
            data = payload,
        };
        db.Set<OutboxEvent>().Add(new OutboxEvent
        {
            Id = eventId,
            OwnerId = ownerId,
            Type = AutomationEvents.DocumentFinalizedName,
            PayloadJson = JsonSerializer.Serialize(envelope, _jsonOptions),
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static MutationOutcome ToFailedOutcome(GenerateResult generated)
    {
        var (code, message, status) = generated.ErrorKind switch
        {
            GenerationErrorKind.NotFound => ("not_found", generated.Error ?? "Not found.", 404),
            GenerationErrorKind.Forbidden => ("forbidden", generated.Error ?? "Forbidden.", 403),
            GenerationErrorKind.Validation => ("validation_failed", generated.Error ?? "Invalid.", 422),
            _ => ("render_error", generated.Error ?? "Rendering failed.", 500),
        };
        return new MutationOutcome(status, SerializeError(code, message, null), null, Succeeded: false);
    }

    private static string SerializeError(string code, string message, IReadOnlyList<DocumentFieldError>? fields)
    {
        return JsonSerializer.Serialize(new AutomationError(new AutomationErrorDetail(code, message, fields)), _jsonOptions);
    }

    private sealed record PreValidation(
        GenerationErrorKind ErrorKind,
        string? ErrorCode,
        string? Error,
        IReadOnlyList<DocumentFieldError>? FieldErrors);
}
