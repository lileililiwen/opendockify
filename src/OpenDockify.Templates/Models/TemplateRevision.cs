namespace OpenDockify.Templates.Models;

public sealed class TemplateRevision
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int Revision { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RiskNoticeText { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = string.Empty;
    public string? SourceInstance { get; set; }
    public Guid? SourceStableId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
