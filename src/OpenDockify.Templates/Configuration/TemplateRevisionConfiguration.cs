using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Templates.Models;

namespace OpenDockify.Templates.Configuration;

public sealed class TemplateRevisionConfiguration : IEntityTypeConfiguration<TemplateRevision>
{
    public void Configure(EntityTypeBuilder<TemplateRevision> builder)
    {
        builder.ToTable("TemplateRevisions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TemplateId, x.Revision }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.RiskNoticeText).HasMaxLength(4000);
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.DefinitionJson).IsRequired();
        builder.Property(x => x.SourceInstance).HasMaxLength(200);
    }
}
