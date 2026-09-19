using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Auditing.Contracts;

namespace OpenDockify.Data.Models;

/// <summary>
/// Application-owned durable audit record. The platform
/// <see cref="IAuditSink"/> implementation writes here; admin endpoints
/// project from this table. The normalized shape matches the platform
/// <c>AuditEvent</c> contract; non-sensitive metadata is serialized as
/// JSON so the table stays flat for the documented paged projection.
/// <see cref="OccurredAt"/> is a UTC <see cref="DateTime"/> (not
/// <see cref="DateTimeOffset"/>): SQLite cannot ORDER BY a
/// <c>DateTimeOffset</c> column, and the paged export requires
/// server-side newest-first ordering on every supported provider.
/// </summary>
public sealed class AuditEvent
{
    public Guid Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string? CorrelationId { get; set; }

    public string? SubjectId { get; set; }

    public string? TenantId { get; set; }

    public string? Source { get; set; }

    public string? MetadataJson { get; set; }

    public static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
            return "{}";
        return JsonSerializer.Serialize(metadata);
    }
}

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Action).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Severity).IsRequired().HasMaxLength(20);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.SubjectId).HasMaxLength(64);
        builder.Property(x => x.TenantId).HasMaxLength(64);
        builder.Property(x => x.Source).HasMaxLength(200);
        builder.Property(x => x.MetadataJson).HasColumnType("TEXT");
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.SubjectId);
        builder.HasIndex(x => new { x.Category, x.OccurredAt });
        builder.HasIndex(x => new { x.Action, x.OccurredAt });
    }
}
