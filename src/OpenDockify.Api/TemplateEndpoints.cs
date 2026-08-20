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

        return endpoints;
    }

    private static Guid CurrentUserId(HttpContext http)
    {
        return CurrentUserContextFactory.FromClaims(http.User).UserId;
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
