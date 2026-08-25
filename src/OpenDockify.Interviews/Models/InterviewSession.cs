namespace OpenDockify.Interviews.Models;

public sealed class InterviewSession
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid TemplateId { get; set; }

    public Guid? TemplateRevisionId { get; set; }

    public DateTimeOffset TemplateRevisionStamp { get; set; }

    public string CurrentStepId { get; set; } = string.Empty;

    public string AnswersJson { get; set; } = "{}";

    public string SelectedClauseIdsJson { get; set; } = "[]";

    public int Version { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
