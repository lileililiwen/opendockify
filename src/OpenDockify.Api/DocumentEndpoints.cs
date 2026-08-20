using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Auth;
using OpenDockify.Generation.Models;
using OpenDockify.Generation.Services;

namespace OpenDockify.Api;

/// <summary>
/// Document endpoints: generate, re-edit, paginated list, get, download PDF,
/// and delete — all scoped to the authenticated owner (foreign ids → 404).
/// </summary>
public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/documents").RequireAuthorization();

        group.MapGet("", async (
            HttpContext http,
            DocumentService documents,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var result = await documents.ListAsync(CurrentUserId(http), page, pageSize, ct);
            return Results.Ok(new
            {
                items = result.Items.Select(ToSummary),
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages,
            });
        });

        group.MapPost("/generate", async (
            HttpContext http,
            GenerateDocumentRequest request,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var command = new GenerateCommand(request.TemplateId, request.Values, request.SelectedClauseIds);
            var result = await documents.GenerateAsync(CurrentUserId(http), command, ct);
            return ToGenerateResult(result);
        });

        group.MapGet("/{id:guid}", async (
            HttpContext http,
            Guid id,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var result = await documents.GetAsync(CurrentUserId(http), id, ct);
            return result.NotFound
                ? Results.Json(new { error = "Document not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(ToView(result.Value!));
        });

        group.MapPost("/{id:guid}/reedit", async (
            HttpContext http,
            Guid id,
            GenerateDocumentRequest request,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var command = new GenerateCommand(request.TemplateId, request.Values, request.SelectedClauseIds);
            var result = await documents.ReEditAsync(CurrentUserId(http), id, command, ct);
            return ToGenerateResult(result);
        });

        group.MapGet("/{id:guid}/download", async (
            HttpContext http,
            Guid id,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var result = await documents.GetAsync(CurrentUserId(http), id, ct);
            if (result.NotFound)
            {
                return Results.Json(new { error = "Document not found." }, statusCode: StatusCodes.Status404NotFound);
            }

            var path = result.Value!.PdfPath;
            if (!File.Exists(path))
            {
                return Results.Json(new { error = "PDF file is missing." }, statusCode: StatusCodes.Status404NotFound);
            }

            // No range processing: the PDF is streamed whole.
            return Results.File(path, "application/pdf", fileDownloadName: $"{id}.pdf", enableRangeProcessing: false);
        });

        group.MapDelete("/{id:guid}", async (
            HttpContext http,
            Guid id,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var result = await documents.DeleteAsync(CurrentUserId(http), id, ct);
            return result.NotFound
                ? Results.Json(new { error = "Document not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.NoContent();
        });

        return endpoints;
    }

    private static Guid CurrentUserId(HttpContext http)
    {
        return CurrentUserContextFactory.FromClaims(http.User).UserId;
    }

    private static IResult ToGenerateResult(GenerateResult result)
    {
        return result.ErrorKind switch
        {
            GenerationErrorKind.None => Results.Json(new
            {
                document = ToView(result.Document!),
                warnings = result.Warnings,
                downloadUrl = $"/api/documents/{result.Document!.Id}/download",
            }),
            GenerationErrorKind.NotFound => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status404NotFound),
            GenerationErrorKind.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
            GenerationErrorKind.Validation => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status400BadRequest),
            GenerationErrorKind.RenderError => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static object ToSummary(Document document)
    {
        return new
        {
            document.Id,
            document.TemplateId,
            document.ParentId,
            document.Status,
            document.CreatedAt,
        };
    }

    private static object ToView(Document document)
    {
        return new
        {
            document.Id,
            document.TemplateId,
            document.ParentId,
            document.Status,
            document.SnapshotJson,
            document.RenderedText,
            document.CreatedAt,
            downloadUrl = $"/api/documents/{document.Id}/download",
        };
    }
}

public sealed record GenerateDocumentRequest(
    Guid TemplateId,
    Dictionary<string, string> Values,
    List<string> SelectedClauseIds);
