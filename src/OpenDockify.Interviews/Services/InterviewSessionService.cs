using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenDockify.Generation.Services;
using OpenDockify.Interviews.Models;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;

namespace OpenDockify.Interviews.Services;

public enum InterviewErrorKind
{
    None,
    NotFound,
    Validation,
    Conflict,
}

public sealed record InterviewStepView(
    string Id,
    string Title,
    string? ReviewLabel,
    IReadOnlyList<FieldDefinition> Fields,
    int Position,
    int Total);

public sealed record InterviewSessionView(
    Guid Id,
    Guid TemplateId,
    int Version,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> Answers,
    IReadOnlyList<string> SelectedClauseIds,
    InterviewStepView? CurrentStep,
    bool ReadyForReview);

public sealed record InterviewReviewItem(string StepId, string Label, IReadOnlyDictionary<string, string> Answers);

public sealed record InterviewResult(
    InterviewSessionView? Session,
    IReadOnlyList<InterviewReviewItem> Review,
    DocumentPreviewResult? Preview,
    InterviewErrorKind ErrorKind,
    string? Error)
{
    public static InterviewResult Failure(InterviewErrorKind kind, string error)
    {
        return new(null, [], null, kind, error);
    }

    public static InterviewResult Success(InterviewSessionView session)
    {
        return new(session, [], null, InterviewErrorKind.None, null);
    }
}

public sealed class InterviewSessionService(
    DbContext db,
    TemplateService templates,
    DocumentService documents,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InterviewResult> CreateAsync(
        Guid ownerId,
        Guid templateId,
        IReadOnlyList<string>? selectedClauseIds,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadTemplateAsync(ownerId, templateId, cancellationToken);
        if (loaded.Error is not null)
        {
            return loaded.Error;
        }

        if (loaded.Value is not { } templateData)
        {
            return InterviewResult.Failure(InterviewErrorKind.Validation, "Template interview could not be loaded.");
        }

        var (template, definition) = templateData;
        var interview = definition.Interview;
        if (interview is null)
        {
            return InterviewResult.Failure(InterviewErrorKind.Validation, "Template has no guided interview.");
        }
        var clauseError = ValidateClauses(definition, selectedClauseIds ?? []);
        if (clauseError is not null)
        {
            return InterviewResult.Failure(InterviewErrorKind.Validation, clauseError);
        }

        var revisionId = await db.Set<TemplateRevision>()
            .Where(x => x.TemplateId == template.Id && x.Revision == template.CurrentRevision)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var session = new InterviewSession
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            TemplateId = template.Id,
            TemplateRevisionId = revisionId,
            TemplateRevisionStamp = template.UpdatedAt,
            CurrentStepId = interview.StartStepId,
            SelectedClauseIdsJson = JsonSerializer.Serialize(selectedClauseIds ?? [], _jsonOptions),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(SessionLifetimeHours()),
        };
        db.Set<InterviewSession>().Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return InterviewResult.Success(ToView(session, definition));
    }

    public async Task<InterviewResult> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var loaded = await LoadSessionAsync(ownerId, id, cancellationToken);
        return loaded.Error ?? InterviewResult.Success(ToView(loaded.Session!, loaded.Definition!));
    }

    public async Task<InterviewResult> AnswerAsync(
        Guid ownerId,
        Guid id,
        int expectedVersion,
        IReadOnlyDictionary<string, string> submitted,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadSessionAsync(ownerId, id, cancellationToken);
        if (loaded.Error is not null)
        {
            return loaded.Error;
        }

        var session = loaded.Session!;
        var definition = loaded.Definition!;
        if (session.Version != expectedVersion)
        {
            return InterviewResult.Failure(InterviewErrorKind.Conflict, "Interview session was updated by another request.");
        }

        var answers = DeserializeAnswers(session.AnswersJson);
        var visible = InterviewFlow.VisibleSteps(definition.Interview!, answers);
        var current = visible.SingleOrDefault(step => step.Id == session.CurrentStepId);
        if (current is null)
        {
            return InterviewResult.Failure(InterviewErrorKind.Validation, "Answers may only target fields in the current visible step.");
        }

        if (submitted.Keys.Any(field => !current.Fields.Contains(field, StringComparer.Ordinal)))
        {
            return InterviewResult.Failure(InterviewErrorKind.Validation, "Answers may only target fields in the current visible step.");
        }

        var fields = definition.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        foreach (var fieldName in current.Fields)
        {
            submitted.TryGetValue(fieldName, out var value);
            var error = InterviewFlow.ValidateValue(fields[fieldName], value);
            if (error is not null)
            {
                return InterviewResult.Failure(InterviewErrorKind.Validation, error);
            }

            if (value is null)
            {
                answers.Remove(fieldName);
            }
            else
            {
                answers[fieldName] = value;
            }
        }

        answers = InterviewFlow.RemoveHiddenAnswers(definition.Interview!, answers);
        visible = InterviewFlow.VisibleSteps(definition.Interview!, answers);
        var position = visible.ToList().FindIndex(step => step.Id == current.Id);
        session.CurrentStepId = position >= 0 && position + 1 < visible.Count ? visible[position + 1].Id : string.Empty;
        session.AnswersJson = JsonSerializer.Serialize(answers, _jsonOptions);
        session.Version++;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveAsync(cancellationToken))
        {
            return InterviewResult.Failure(InterviewErrorKind.Conflict, "Interview session was updated by another request.");
        }

        return InterviewResult.Success(ToView(session, definition));
    }

    public async Task<InterviewResult> BackAsync(
        Guid ownerId,
        Guid id,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadSessionAsync(ownerId, id, cancellationToken);
        if (loaded.Error is not null)
        {
            return loaded.Error;
        }

        var session = loaded.Session!;
        if (session.Version != expectedVersion)
        {
            return InterviewResult.Failure(InterviewErrorKind.Conflict, "Interview session was updated by another request.");
        }

        var definition = loaded.Definition!;
        var visible = InterviewFlow.VisibleSteps(definition.Interview!, DeserializeAnswers(session.AnswersJson));
        var position = session.CurrentStepId.Length == 0
            ? visible.Count
            : visible.ToList().FindIndex(step => step.Id == session.CurrentStepId);
        if (position <= 0)
        {
            return InterviewResult.Success(ToView(session, definition));
        }

        session.CurrentStepId = visible[position - 1].Id;
        session.Version++;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveAsync(cancellationToken))
        {
            return InterviewResult.Failure(InterviewErrorKind.Conflict, "Interview session was updated by another request.");
        }

        return InterviewResult.Success(ToView(session, definition));
    }

    public async Task<InterviewResult> ReviewAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var loaded = await LoadSessionAsync(ownerId, id, cancellationToken);
        if (loaded.Error is not null)
        {
            return loaded.Error;
        }

        var session = loaded.Session!;
        var definition = loaded.Definition!;
        var view = ToView(session, definition);
        if (!view.ReadyForReview)
        {
            return InterviewResult.Failure(InterviewErrorKind.Validation, "Complete every visible interview step before review.");
        }

        var fields = definition.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var review = InterviewFlow.VisibleSteps(definition.Interview!, view.Answers)
            .Select(step => new InterviewReviewItem(
                step.Id,
                step.ReviewLabel ?? step.Title,
                step.Fields.ToDictionary(field => fields[field].Label, field => view.Answers.GetValueOrDefault(field, string.Empty), StringComparer.Ordinal)))
            .ToList();
        return new InterviewResult(view, review, null, InterviewErrorKind.None, null);
    }

    public async Task<InterviewResult> CompleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var reviewed = await ReviewAsync(ownerId, id, cancellationToken);
        if (reviewed.ErrorKind != InterviewErrorKind.None)
        {
            return reviewed;
        }

        var session = await db.Set<InterviewSession>().SingleAsync(item => item.Id == id, cancellationToken);
        var template = await db.Set<Template>().SingleAsync(item => item.Id == session.TemplateId, cancellationToken);
        if (!InterviewSessionPolicy.MatchesTemplateRevision(session, template.UpdatedAt))
        {
            return InterviewResult.Failure(InterviewErrorKind.Conflict, "Template changed during this session; answers remain available for export.");
        }

        var preview = await documents.PreviewAsync(
            ownerId,
            new GenerateCommand(session.TemplateId, DeserializeAnswers(session.AnswersJson), DeserializeClauses(session.SelectedClauseIdsJson)),
            cancellationToken);
        return preview.ErrorKind == GenerationErrorKind.None
            ? reviewed with { Preview = preview }
            : InterviewResult.Failure(InterviewErrorKind.Validation, preview.Error ?? "Preview failed.");
    }

    public async Task<InterviewResult> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var session = await db.Set<InterviewSession>()
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);
        if (session is null)
        {
            return InterviewResult.Failure(InterviewErrorKind.NotFound, "Interview session not found.");
        }

        db.Set<InterviewSession>().Remove(session);
        await db.SaveChangesAsync(cancellationToken);
        return new InterviewResult(null, [], null, InterviewErrorKind.None, null);
    }

    private async Task<(InterviewSession? Session, TemplateDefinition? Definition, InterviewResult? Error)> LoadSessionAsync(
        Guid ownerId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var session = await db.Set<InterviewSession>()
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);
        if (session is null || !InterviewSessionPolicy.CanAccess(session, ownerId, DateTimeOffset.UtcNow))
        {
            return (null, null, InterviewResult.Failure(InterviewErrorKind.NotFound, "Interview session not found."));
        }

        var loaded = await LoadTemplateAsync(ownerId, session.TemplateId, cancellationToken);
        return loaded.Error is not null
            ? (null, null, loaded.Error)
            : (session, loaded.Value!.Value.Definition, null);
    }

    private async Task<((Template Template, TemplateDefinition Definition)? Value, InterviewResult? Error)> LoadTemplateAsync(
        Guid ownerId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var result = await templates.GetByIdAsync(ownerId, templateId, cancellationToken);
        if (result.NotFound)
        {
            return (null, InterviewResult.Failure(InterviewErrorKind.NotFound, "Template not found."));
        }

        var template = result.Value!;
        var validation = TemplateDefinitionValidator.Validate(template.DefinitionJson, template.Body);
        if (!validation.IsValid || validation.Definition!.Interview is null)
        {
            return (null, InterviewResult.Failure(InterviewErrorKind.Validation, validation.Error ?? "Template has no guided interview."));
        }

        return ((template, validation.Definition), null);
    }

    private static string? ValidateClauses(TemplateDefinition definition, IReadOnlyList<string> selected)
    {
        var allowed = definition.Clauses.Select(clause => clause.Id).ToHashSet(StringComparer.Ordinal);
        return selected.FirstOrDefault(id => !allowed.Contains(id)) is string invalid
            ? $"Unknown clause id '{invalid}'."
            : null;
    }

    private static Dictionary<string, string> DeserializeAnswers(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions) ?? new(StringComparer.Ordinal);
    }

    private static List<string> DeserializeClauses(string json)
    {
        return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? [];
    }

    private static InterviewSessionView ToView(InterviewSession session, TemplateDefinition definition)
    {
        var answers = DeserializeAnswers(session.AnswersJson);
        var visible = InterviewFlow.VisibleSteps(definition.Interview!, answers);
        var position = visible.ToList().FindIndex(step => step.Id == session.CurrentStepId);
        var current = position < 0 ? null : visible[position];
        var fields = definition.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var stepView = current is null
            ? null
            : new InterviewStepView(current.Id, current.Title, current.ReviewLabel, current.Fields.Select(name => fields[name]).ToList(), position + 1, visible.Count);
        return new InterviewSessionView(
            session.Id,
            session.TemplateId,
            session.Version,
            session.ExpiresAt,
            answers,
            DeserializeClauses(session.SelectedClauseIdsJson),
            stepView,
            current is null);
    }

    private async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private int SessionLifetimeHours()
    {
        return int.TryParse(configuration["Interviews:SessionLifetimeHours"], out var hours)
            ? Math.Clamp(hours, 1, 720)
            : 168;
    }
}
