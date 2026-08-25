using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDockify.Auth;
using OpenDockify.Interviews.Services;

namespace OpenDockify.Api;

public static class InterviewEndpoints
{
    public static IEndpointRouteBuilder MapInterviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/interviews").RequireAuthorization();

        group.MapPost("", async (
            HttpContext http,
            CreateInterviewRequest request,
            InterviewSessionService interviews,
            CancellationToken cancellationToken) =>
            ToResult(await interviews.CreateAsync(CurrentUserId(http), request.TemplateId, request.SelectedClauseIds, cancellationToken), true));

        group.MapGet("/{id:guid}", async (
            HttpContext http,
            Guid id,
            InterviewSessionService interviews,
            CancellationToken cancellationToken) =>
            ToResult(await interviews.GetAsync(CurrentUserId(http), id, cancellationToken)));

        group.MapPut("/{id:guid}/answer", async (
            HttpContext http,
            Guid id,
            AnswerInterviewRequest request,
            InterviewSessionService interviews,
            CancellationToken cancellationToken) =>
            ToResult(await interviews.AnswerAsync(CurrentUserId(http), id, request.ExpectedVersion, request.Answers, cancellationToken)));

        group.MapPost("/{id:guid}/back", async (
            HttpContext http,
            Guid id,
            VersionedInterviewRequest request,
            InterviewSessionService interviews,
            CancellationToken cancellationToken) =>
            ToResult(await interviews.BackAsync(CurrentUserId(http), id, request.ExpectedVersion, cancellationToken)));

        group.MapGet("/{id:guid}/review", async (
            HttpContext http,
            Guid id,
            InterviewSessionService interviews,
            CancellationToken cancellationToken) =>
            ToResult(await interviews.ReviewAsync(CurrentUserId(http), id, cancellationToken)));

        group.MapPost("/{id:guid}/complete", async (
            HttpContext http,
            Guid id,
            InterviewSessionService interviews,
            CancellationToken cancellationToken) =>
            ToResult(await interviews.CompleteAsync(CurrentUserId(http), id, cancellationToken)));

        group.MapDelete("/{id:guid}", async (
            HttpContext http,
            Guid id,
            InterviewSessionService interviews,
            CancellationToken cancellationToken) =>
        {
            var result = await interviews.DeleteAsync(CurrentUserId(http), id, cancellationToken);
            return result.ErrorKind == InterviewErrorKind.None ? Results.NoContent() : ToResult(result);
        });

        return endpoints;
    }

    private static IResult ToResult(InterviewResult result, bool created = false)
    {
        if (result.ErrorKind != InterviewErrorKind.None)
        {
            return result.ErrorKind switch
            {
                InterviewErrorKind.NotFound => Results.NotFound(new { error = result.Error }),
                InterviewErrorKind.Conflict => Results.Conflict(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error }),
            };
        }

        var body = new { session = result.Session, review = result.Review, preview = result.Preview };
        return created ? Results.Created($"/api/interviews/{result.Session!.Id}", body) : Results.Ok(body);
    }

    private static Guid CurrentUserId(HttpContext http)
    {
        return CurrentUserContextFactory.FromClaims(http.User).UserId;
    }
}

public sealed record CreateInterviewRequest(Guid TemplateId, List<string>? SelectedClauseIds);

public sealed record AnswerInterviewRequest(int ExpectedVersion, Dictionary<string, string> Answers);

public sealed record VersionedInterviewRequest(int ExpectedVersion);
