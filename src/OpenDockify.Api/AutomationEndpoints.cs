using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using OpenDockify.Generation.Models;
using OpenDockify.Generation.Services;
using OpenDockify.Integrations.Services;
using Platform.Storage.Contracts;
using Platform.Storage.Keys;

namespace OpenDockify.Api;

/// <summary>
/// Versioned automation API for machine clients (service tokens). Every
/// endpoint requires exactly one token scope; mutations require an
/// Idempotency-Key header and return stable, replayable JSON bodies.
/// </summary>
public static class AutomationEndpoints
{
    public const string RateLimitPolicy = "automation-api";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/automation").RequireRateLimiting(RateLimitPolicy);

        // Machine-readable contract; unauthenticated so SDK generators can
        // fetch it before issuing tokens.
        group.MapGet("/openapi.json", () =>
            Results.Bytes(AutomationContract.DocumentBytes, "application/json"));

        group.MapGet("/templates", async (
            HttpContext http,
            AutomationService automation,
            CancellationToken ct) =>
            Results.Ok(await automation.ListTemplatesAsync(OwnerId(http), ct)))
            .RequireAuthorization(AutomationAuthorization.PolicyFor(AutomationScopes.TemplatesRead));

        group.MapPost("/preview", async (
            HttpContext http,
            GenerateDocumentRequest request,
            AutomationService automation,
            NotifyRateGate gate,
            CancellationToken ct) =>
        {
            var rate = await gate.CheckRateAsync("preview", http, ct);
            if (!rate.Allowed)
                return NotifyRateGate.RateLimitedResult(rate);
            var result = await automation.PreviewAsync(
                OwnerId(http),
                new GenerateCommand(request.TemplateId, request.Values ?? [], request.SelectedClauseIds ?? []),
                ct);
            return result.ErrorKind switch
            {
                GenerationErrorKind.None => Results.Ok(new
                {
                    renderedText = result.RenderedText,
                    templateName = result.TemplateName,
                    warnings = result.Warnings,
                }),
                GenerationErrorKind.NotFound => ErrorContent(
                    "not_found", result.Error ?? "Template not found.", null, StatusCodes.Status404NotFound),
                _ => ErrorContent(
                    "validation_failed", result.Error ?? "Invalid request.", result.FieldErrors,
                    StatusCodes.Status422UnprocessableEntity),
            };
        })
        .RequireAuthorization(AutomationAuthorization.PolicyFor(AutomationScopes.DocumentsPreview));

        group.MapPost("/finalize", async (
            HttpContext http,
            AutomationService automation,
            NotifyRateGate gate,
            PlatformIdempotencyBridge bridge,
            PlatformOutboxBridge outbox,
            NotifyService notify,
            CancellationToken ct) =>
        {
            var rate = await gate.CheckRateAsync("finalize", http, ct);
            if (!rate.Allowed)
                return NotifyRateGate.RateLimitedResult(rate);
            var (quotaOk, quotaReject) = await gate.CheckDocumentQuotaAsync(http, ct);
            if (!quotaOk)
                return quotaReject!;
            var key = http.Request.Headers["Idempotency-Key"].ToString().Trim();
            if (key.Length == 0)
            {
                return ErrorContent(
                    "missing_idempotency_key", "The Idempotency-Key header is required.", null,
                    StatusCodes.Status400BadRequest);
            }

            // Keys are opaque: bounded length and a conservative character set.
            if (key.Length is < 8 or > 128 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            {
                return ErrorContent(
                    "invalid_idempotency_key", "The Idempotency-Key must be 8-128 ASCII letters, digits, '-', or '_'.", null,
                    StatusCodes.Status400BadRequest);
            }

            http.Request.EnableBuffering();
            using var reader = new StreamReader(
                http.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: -1,
                leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(ct);
            http.Request.Body.Position = 0;

            AutomationFinalizeRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<AutomationFinalizeRequest>(rawBody, _jsonOptions);
            }
            catch (JsonException)
            {
                request = null;
            }

            if (request is null || request.TemplateId == Guid.Empty)
            {
                return ErrorContent(
                    "invalid_request", "The request body must include a templateId.", null,
                    StatusCodes.Status400BadRequest);
            }

            var digest = RequestDigests.Compute(Encoding.UTF8.GetBytes(rawBody));
            var replay = await bridge.TryReplayAsync(TokenId(http), key, AutomationService.FinalizeRoute, digest, ct);
            if (replay.Hit)
            {
                return Results.Content(replay.Body!, "application/json", Encoding.UTF8, replay.Status);
            }

            if (replay.Status == 409)
            {
                return ErrorContent("conflict_idempotency_key", "This idempotency key was already used with a different request body.", null, StatusCodes.Status409Conflict);
            }

            var result = await automation.FinalizeAsync(
                OwnerId(http),
                TokenId(http),
                key,
                AutomationService.FinalizeRoute,
                digest,
                new GenerateCommand(request.TemplateId, request.Values ?? [], request.SelectedClauseIds ?? []),
                ct);

            await bridge.SaveAsync(TokenId(http), key, AutomationService.FinalizeRoute, digest, result.StatusCode, result.ResponseJson, ct);
            if (result.StatusCode == 201)
            {
                await gate.ConsumeDocumentAsync(http, ct);
                await outbox.PublishFinalizedAsync(Guid.NewGuid(), OwnerId(http), result.ResponseJson, http.TraceIdentifier, ct);
                await notify.SendDocumentFinalizedAsync(OwnerId(http).ToString(), request.TemplateId.ToString(), result.DocumentId ?? Guid.Empty, ct);
            }

            return Results.Content(result.ResponseJson, "application/json", Encoding.UTF8, result.StatusCode);
        })
        .RequireAuthorization(AutomationAuthorization.PolicyFor(AutomationScopes.DocumentsWrite));

        group.MapGet("/operations/{id:guid}", async (
            HttpContext http,
            Guid id,
            AutomationService automation,
            CancellationToken ct) =>
        {
            var operation = await automation.GetOperationAsync(OwnerId(http), id, ct);
            return operation is null
                ? ErrorContent("not_found", "Operation not found.", null, StatusCodes.Status404NotFound)
                : Results.Ok(operation);
        })
        .RequireAuthorization(AutomationAuthorization.PolicyFor(AutomationScopes.OperationsRead));

        group.MapGet("/documents/{id:guid}", async (
            HttpContext http,
            Guid id,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var access = await documents.GetAsync(OwnerId(http), id, ct);
            return access.NotFound
                ? ErrorContent("not_found", "Document not found.", null, StatusCodes.Status404NotFound)
                : Results.Ok(ToDocumentView(access.Value!));
        })
        .RequireAuthorization(AutomationAuthorization.PolicyFor(AutomationScopes.DocumentsRead));

        group.MapGet("/documents/{id:guid}/pdf", async (
            HttpContext http,
            Guid id,
            DocumentService documents,
            IObjectStorage storage,
            CancellationToken ct) =>
        {
            var access = await documents.GetAsync(OwnerId(http), id, ct);
            if (access.NotFound)
            {
                return ErrorContent("not_found", "Document not found.", null, StatusCodes.Status404NotFound);
            }

            var document = access.Value!.Document;
            var key = !string.IsNullOrEmpty(document.PdfStorageKey) ? document.PdfStorageKey : null;
            if (key is null)
            {
                return ErrorContent("not_found", "PDF file is missing.", null, StatusCodes.Status404NotFound);
            }

            // 15-minute presigned operation; never exposes bucket credentials.
            var presign = await storage.PresignAsync(
                new PresignRequest(new StorageObjectKey(key), StorageOperation.Download, TimeSpan.FromMinutes(15)),
                ct);
            if (presign.Outcome.Status != StorageOutcomeStatus.Succeeded || presign.Operation is null)
            {
                return ErrorContent("storage_unavailable", "The object storage provider is unavailable.", null, StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                documentId = document.Id,
                pdfUrl = presign.Operation.Url,
                expiresAt = presign.Operation.ExpiresAt,
            });
        })
        .RequireAuthorization(AutomationAuthorization.PolicyFor(AutomationScopes.DocumentsRead));

        return endpoints;
    }

    internal static object ToDocumentView(DocumentLibraryDetail detail)
    {
        return new
        {
            detail.Document.Id,
            Title = detail.Title,
            detail.Document.TemplateId,
            TemplateName = detail.TemplateName,
            detail.Document.IsArchived,
            Status = detail.Document.Status.ToString(),
            SigningStatus = detail.Document.SigningStatus.ToString(),
            detail.Document.ContentSha256,
            detail.Document.CreatedAt,
            pdfUrl = $"/api/v1/automation/documents/{detail.Document.Id}/pdf",
        };
    }

    internal static IResult ErrorContent(string code, string message, IReadOnlyList<DocumentFieldError>? fields, int statusCode)
    {
        // The error body is serialized once here so replayed and fresh
        // responses carry identical bytes.
        return Results.Content(SerializeError(code, message, fields), "application/json", Encoding.UTF8, statusCode);
    }

    internal static string SerializeError(string code, string message, IReadOnlyList<DocumentFieldError>? fields)
    {
        return JsonSerializer.Serialize(
            new AutomationError(new AutomationErrorDetail(code, message, fields)),
            _jsonOptions);
    }

    internal static Guid OwnerId(HttpContext http)
    {
        return Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated principal lacks a name identifier."));
    }

    internal static Guid TokenId(HttpContext http)
    {
        return Guid.Parse(http.User.FindFirstValue("token_id")
            ?? throw new InvalidOperationException("Authenticated principal lacks a token id."));
    }
}

public sealed record AutomationFinalizeRequest(
    Guid TemplateId,
    Dictionary<string, string>? Values,
    List<string>? SelectedClauseIds);
