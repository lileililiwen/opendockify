using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Interviews.Models;

namespace OpenDockify.Interviews.Configuration;

public sealed class InterviewSessionConfiguration : IEntityTypeConfiguration<InterviewSession>
{
    public void Configure(EntityTypeBuilder<InterviewSession> builder)
    {
        builder.ToTable("InterviewSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.OwnerId).IsRequired();
        builder.Property(session => session.TemplateId).IsRequired();
        builder.Property(session => session.TemplateRevisionStamp).IsRequired();
        builder.HasIndex(session => session.TemplateRevisionId);
        builder.Property(session => session.CurrentStepId).HasMaxLength(100).IsRequired();
        builder.Property(session => session.AnswersJson).HasMaxLength(100_000).IsRequired();
        builder.Property(session => session.SelectedClauseIdsJson).HasMaxLength(10_000).IsRequired();
        builder.Property(session => session.Version).IsConcurrencyToken().IsRequired();
        builder.Property(session => session.ExpiresAt).IsRequired();
        builder.Property(session => session.CreatedAt).IsRequired();
        builder.Property(session => session.UpdatedAt).IsRequired();
        builder.HasIndex(session => new { session.OwnerId, session.ExpiresAt });
        builder.HasIndex(session => session.TemplateId);
    }
}
