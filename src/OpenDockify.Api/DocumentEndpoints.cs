using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Auth;
using OpenDockify.Generation.Models;
using OpenDockify.Generation.Services;
using Platform.Storage.Contracts;

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
            string? search = null,
            string? archive = null,
            string? sort = null,
            CancellationToken ct = default) =>
        {
            if (!DocumentLibraryQuery.TryCreate(search, archive, sort, out var libraryQuery, out var error))
            {
                return Results.Json(new { error }, statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await documents.ListAsync(CurrentUserId(http), page, pageSize, libraryQuery, ct);
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

        group.MapPost("/finalize", async (
            HttpContext http,
            GenerateDocumentRequest request,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var command = new GenerateCommand(request.TemplateId, request.Values, request.SelectedClauseIds);
            var result = await documents.GenerateAsync(CurrentUserId(http), command, ct);
            return ToGenerateResult(result);
        });

        group.MapPost("/preview", async (
            HttpContext http,
            GenerateDocumentRequest request,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var command = new GenerateCommand(request.TemplateId, request.Values, request.SelectedClauseIds);
            var result = await documents.PreviewAsync(CurrentUserId(http), command, ct);
            return ToPreviewResult(result);
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

        group.MapPut("/{id:guid}/metadata", async (
            HttpContext http,
            Guid id,
            UpdateDocumentMetadataRequest request,
            DocumentService documents,
            CancellationToken ct) =>
        {
            if (await IsSharedNonOwnerAsync(http, id, documents, ct))
            {
                return Results.Json(new { error = "Only the document owner may modify it." }, statusCode: StatusCodes.Status403Forbidden);
            }
            var result = await documents.UpdateMetadataAsync(
                CurrentUserId(http), id, request.Title, request.IsArchived, ct);
            return result.ErrorKind switch
            {
                DocumentMetadataErrorKind.None => Results.Ok(ToView(result.Detail!)),
                DocumentMetadataErrorKind.NotFound => Results.Json(
                    new { error = result.Error }, statusCode: StatusCodes.Status404NotFound),
                DocumentMetadataErrorKind.Validation => Results.Json(
                    new { error = result.Error }, statusCode: StatusCodes.Status400BadRequest),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        });

        group.MapGet("/{id:guid}/versions", async (
            HttpContext http,
            Guid id,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var result = await documents.GetVersionHistoryAsync(CurrentUserId(http), id, ct);
            return result.NotFound
                ? Results.Json(new { error = "Document not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(result.Items.Select(ToSummary));
        });

        group.MapPost("/{id:guid}/reedit", async (
            HttpContext http,
            Guid id,
            GenerateDocumentRequest request,
            DocumentService documents,
            CancellationToken ct) =>
        {
            if (await IsSharedNonOwnerAsync(http, id, documents, ct))
            {
                return Results.Json(new { error = "Only the document owner may re-edit it." }, statusCode: StatusCodes.Status403Forbidden);
            }
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

            var document = result.Value!.Document;
            var stored = await documents.TryOpenPdfAsync(document, ct);
            if (stored is null)
            {
                return Results.Json(new { error = "PDF file is missing." }, statusCode: StatusCodes.Status404NotFound);
            }

            await using var _ = stored;
            return await RangeAwarePdfResultAsync(http, stored, $"{id}.pdf");
        });

        group.MapDelete("/{id:guid}", async (
            HttpContext http,
            Guid id,
            DocumentService documents,
            CancellationToken ct) =>
        {
            if (await IsSharedNonOwnerAsync(http, id, documents, ct))
            {
                return Results.Json(new { error = "Only the document owner may delete it." }, statusCode: StatusCodes.Status403Forbidden);
            }
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

    private static async Task<bool> IsSharedNonOwnerAsync(HttpContext http, Guid id, DocumentService documents, CancellationToken ct)
    {
        var access = await documents.GetAsync(CurrentUserId(http), id, ct);
        return !access.NotFound && !access.Value!.IsOwner;
    }

    private static IResult ToGenerateResult(GenerateResult result)
    {
        return result.ErrorKind switch
        {
            GenerationErrorKind.None => Results.Json(new
            {
                document = ToView(result.Document!, result.TemplateName),
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

    private static IResult ToPreviewResult(DocumentPreviewResult result)
    {
        return result.ErrorKind switch
        {
            GenerationErrorKind.None => Results.Ok(new
            {
                result.TemplateName,
                result.RenderedText,
                result.Warnings,
            }),
            GenerationErrorKind.NotFound => Results.Json(
                new { error = result.Error }, statusCode: StatusCodes.Status404NotFound),
            GenerationErrorKind.Forbidden => Results.Json(
                new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
            GenerationErrorKind.Validation => Results.Json(
                new { error = result.Error }, statusCode: StatusCodes.Status400BadRequest),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static object ToSummary(DocumentLibraryItem document)
    {
        return new
        {
            document.Id,
            document.Title,
            document.TemplateId,
            document.TemplateName,
            document.IsArchived,
            document.ParentId,
            Status = document.Status.ToString(),
            SigningStatus = document.SigningStatus.ToString(),
            document.CreatedAt,
        };
    }

    private static object ToView(DocumentLibraryDetail detail)
    {
        return ToView(detail.Document, detail.TemplateName, detail.Title, detail.IsOwner, detail.AccessLevel);
    }

    private static object ToView(Document document, string? templateName, string? resolvedTitle = null, bool isOwner = true, string accessLevel = "owner")
    {
        return new
        {
            document.Id,
            Title = resolvedTitle ?? document.Title,
            document.TemplateId,
            TemplateName = templateName ?? string.Empty,
            document.IsArchived,
            document.ParentId,
            Status = document.Status.ToString(),
            SigningStatus = document.SigningStatus.ToString(),
            document.SnapshotJson,
            document.RenderedText,
            document.CreatedAt,
            IsOwner = isOwner,
            AccessLevel = accessLevel,
            downloadUrl = $"/api/documents/{document.Id}/download",
        };
    }

    /// <summary>
    /// Streams a stored PDF as the response body with HTTP <c>Range</c>
    /// support. Returns 200 with the full body when no <c>Range</c> header
    /// is supplied and 206 with the requested slice + <c>Content-Range</c>
    /// when one is. Every response advertises <c>Accept-Ranges: bytes</c>.
    /// </summary>
    public static async Task<IResult> RangeAwarePdfResultAsync(HttpContext http, StorageReadResult stored, string downloadFileName)
    {
        var total = stored.Metadata.LengthBytes;
        var range = http.Request.Headers.Range.ToString();
        if (string.IsNullOrWhiteSpace(range))
        {
            http.Response.Headers.AcceptRanges = "bytes";
            http.Response.Headers.LastModified = stored.Metadata.LastModified.ToString("R", CultureInfo.InvariantCulture);
            return Results.Stream(stored.Content, "application/pdf", downloadFileName);
        }

        if (!TryParseRange(range, total, out var start, out var end))
        {
            http.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            http.Response.Headers.ContentRange = $"bytes */{total}";
            return Results.Empty;
        }

        var length = end - start + 1;
        http.Response.StatusCode = StatusCodes.Status206PartialContent;
        http.Response.Headers.AcceptRanges = "bytes";
        http.Response.Headers.ContentRange = $"bytes {start}-{end}/{total}";
        http.Response.Headers.LastModified = stored.Metadata.LastModified.ToString("R", CultureInfo.InvariantCulture);
        http.Response.ContentLength = length;
        http.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{downloadFileName}\"";
        if (start > 0)
        {
            await SkipAsync(stored.Content, start, http.RequestAborted);
        }
        await CopyBoundedAsync(stored.Content, http.Response.Body, length, http.RequestAborted);
        return Results.Empty;
    }

    private static bool TryParseRange(string header, long total, out long start, out long end)
    {
        start = 0;
        end = 0;
        if (!header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var spec = header["bytes=".Length..].Split(',', 2)[0].Trim();
        var dash = spec.IndexOf('-');
        if (dash <= 0)
        {
            return false;
        }

        var startText = spec[..dash].Trim();
        var endText = spec[(dash + 1)..].Trim();
        if (!long.TryParse(startText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStart)
            || parsedStart < 0 || parsedStart >= total)
        {
            return false;
        }
        start = parsedStart;
        if (endText.Length == 0)
        {
            end = total - 1;
        }
        else
        {
            if (!long.TryParse(endText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedEnd)
                || parsedEnd < start || parsedEnd >= total)
            {
                return false;
            }
            end = parsedEnd;
        }
        return true;
    }

    private static async Task SkipAsync(Stream stream, long count, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(count, SeekOrigin.Current);
            return;
        }
        var remaining = count;
        var buffer = new byte[81920];
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read <= 0)
            {
                break;
            }
            remaining -= read;
        }
    }

    private static async Task CopyBoundedAsync(Stream source, Stream destination, long count, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long remaining = count;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read <= 0)
            {
                break;
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }
}

public sealed record GenerateDocumentRequest(
    Guid TemplateId,
    Dictionary<string, string> Values,
    List<string> SelectedClauseIds);

public sealed record UpdateDocumentMetadataRequest(string? Title, bool IsArchived);
