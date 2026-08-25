using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Auth;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;

namespace OpenDockify.Api;

/// <summary>
/// Template endpoints: marketplace, CRUD, copy, and the admin global-template
/// upload. All require authentication; admin upload requires the
/// <c>RequireAdmin</c> policy. Multi-user isolation is enforced by
/// <see cref="TemplateService"/> (foreign private templates → 404).
/// </summary>
public static class TemplateEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/templates").RequireAuthorization();

        group.MapGet("/marketplace", async (HttpContext http, TemplateService templates, CancellationToken ct) =>
        {
            var list = await templates.GetMarketplaceAsync(CurrentUserId(http), ct);
            return Results.Ok(list.Select(ToSummary));
        });

        group.MapGet("/{id:guid}", async (
            HttpContext http,
            Guid id,
            TemplateService templates,
            CancellationToken ct) =>
        {
            var result = await templates.GetByIdAsync(CurrentUserId(http), id, ct);
            return result.NotFound
                ? Results.Json(new { error = "Template not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(ToView(result.Value!));
        });

        group.MapPost("", async (
            HttpContext http,
            CreateTemplateRequest request,
            TemplateService templates,
            CancellationToken ct) =>
        {
            var result = await templates.CreateAsync(CurrentUserId(http), ToDraft(request), ct);
            return ToResult(result, StatusCodes.Status201Created);
        });

        group.MapPut("/{id:guid}", async (
            HttpContext http,
            Guid id,
            CreateTemplateRequest request,
            TemplateService templates,
            CancellationToken ct) =>
        {
            var result = await templates.UpdateAsync(CurrentUserId(http), id, ToDraft(request), ct);
            return ToResult(result);
        });

        group.MapDelete("/{id:guid}", async (
            HttpContext http,
            Guid id,
            TemplateService templates,
            CancellationToken ct) =>
        {
            var result = await templates.DeleteAsync(CurrentUserId(http), id, ct);
            return ToResult(result);
        });

        group.MapPost("/{id:guid}/copy", async (
            HttpContext http,
            Guid id,
            TemplateService templates,
            CancellationToken ct) =>
        {
            var result = await templates.CopyAsync(CurrentUserId(http), id, ct);
            return ToResult(result, StatusCodes.Status201Created);
        });

        group.MapGet("/{id:guid}/export", async (HttpContext http, Guid id, TemplatePackageService packages, CancellationToken ct) =>
        {
            var (bytes, error) = await packages.ExportAsync(CurrentUserId(http), id, ct);
            return bytes is null ? Results.NotFound(new { error }) : Results.File(bytes, "application/vnd.opendockify.template+json", $"template-{id}.json");
        });

        group.MapGet("/{id:guid}/revisions", async (HttpContext http, Guid id, TemplateService templates, CancellationToken ct) =>
        {
            var revisions = await templates.GetRevisionsAsync(CurrentUserId(http), id, ct);
            return revisions.Count == 0 ? Results.NotFound() : Results.Ok(revisions);
        });

        group.MapPost("/{id:guid}/revisions/{revision:int}/rollback", async (HttpContext http, Guid id, int revision, TemplateService templates, CancellationToken ct) =>
            ToResult(await templates.RollbackAsync(CurrentUserId(http), id, revision, ct)));

        return endpoints;
    }

    public static IEndpointRouteBuilder MapAdminTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/templates")
            .RequireAuthorization("RequireAdmin");

        group.MapPost("", async (
            AdminTemplateRequest request,
            TemplateService templates,
            CancellationToken ct) =>
        {
            var result = await templates.AdminUpsertAsync(request.Id, ToDraft(request), ct);
            return ToResult(result);
        });

        group.MapPost("/packages/validate", async (HttpRequest request, TemplatePackageService packages, CancellationToken ct) =>
        {
            var bytes = await ReadPackageAsync(request, ct);
            var result = await packages.ValidateAsync(bytes, ct);
            return result.Valid ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/packages/import", async (HttpContext http, HttpRequest request, string receipt, string policy, TemplatePackageService packages, CancellationToken ct) =>
        {
            var bytes = await ReadPackageAsync(request, ct);
            var result = await packages.ImportAsync(CurrentUserId(http), bytes, receipt, policy, ct);
            if (result.Conflict)
                return Results.Conflict(new { error = result.Error });
            return result.Template is null ? Results.BadRequest(new { error = result.Error }) : Results.Created($"/api/templates/{result.Template.Id}", ToView(result.Template));
        });

        return endpoints;
    }

    private static Guid CurrentUserId(HttpContext http)
    {
        return CurrentUserContextFactory.FromClaims(http.User).UserId;
    }

    private static async Task<byte[]> ReadPackageAsync(HttpRequest request, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        return buffer.Length > TemplatePackageService.MaxPackageBytes ? new byte[TemplatePackageService.MaxPackageBytes + 1] : buffer.ToArray();
    }

    private static TemplateDraft ToDraft(CreateTemplateRequest request)
    {
        return new TemplateDraft(
            request.Name,
            request.Category,
            request.Description ?? string.Empty,
            request.RiskNoticeText ?? string.Empty,
            request.Body,
            request.DefinitionJson);
    }

    private static TemplateDraft ToDraft(AdminTemplateRequest request)
    {
        return new TemplateDraft(
            request.Name,
            request.Category,
            request.Description ?? string.Empty,
            request.RiskNoticeText ?? string.Empty,
            request.Body,
            request.DefinitionJson);
    }

    private static IResult ToResult(TemplateResult result, int successStatus = StatusCodes.Status200OK)
    {
        return result.Error switch
        {
            TemplateErrorKind.None => result.Template is null
                ? Results.NoContent()
                : Results.Json(ToView(result.Template), statusCode: successStatus),
            TemplateErrorKind.Validation => Results.Json(new { error = result.ErrorMessage }, statusCode: StatusCodes.Status400BadRequest),
            TemplateErrorKind.Forbidden => Results.Json(new { error = result.ErrorMessage }, statusCode: StatusCodes.Status403Forbidden),
            TemplateErrorKind.NotFound => Results.Json(new { error = result.ErrorMessage }, statusCode: StatusCodes.Status404NotFound),
            TemplateErrorKind.TooLarge => Results.Json(new { error = result.ErrorMessage }, statusCode: StatusCodes.Status413PayloadTooLarge),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static object ToSummary(Template template)
    {
        return new
        {
            template.Id,
            template.Name,
            template.Category,
            template.Description,
            template.IsBuiltIn,
            template.IsPublic,
            template.OwnerId,
            template.StableId,
            template.CurrentRevision,
            template.SourceInstance,
            template.SourceStableId,
        };
    }

    private static object ToView(Template template)
    {
        return new
        {
            template.Id,
            template.Name,
            template.Category,
            template.Description,
            template.RiskNoticeText,
            template.Body,
            template.DefinitionJson,
            template.IsBuiltIn,
            template.IsPublic,
            template.OwnerId,
            template.StableId,
            template.CurrentRevision,
            template.SourceInstance,
            template.SourceStableId,
            template.CreatedAt,
            template.UpdatedAt,
        };
    }
}

public sealed record CreateTemplateRequest(
    string Name,
    string Category,
    string? Description,
    string? RiskNoticeText,
    string Body,
    string DefinitionJson);

public sealed record AdminTemplateRequest(
    Guid? Id,
    string Name,
    string Category,
    string? Description,
    string? RiskNoticeText,
    string Body,
    string DefinitionJson);
