using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.AiAssist.Models;

namespace OpenDockify.AiAssist.Configuration;

public sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("AiUsageLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserId)
            .IsRequired();

        builder.Property(l => l.Action)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(l => l.RequestSnippet)
            .HasMaxLength(4000);

        builder.Property(l => l.ResponseSnippet)
            .HasMaxLength(4000);

        builder.Property(l => l.Success)
            .IsRequired();

        builder.Property(l => l.Timestamp)
            .IsRequired();

        builder.HasIndex(l => l.UserId);
        builder.HasIndex(l => l.Timestamp);
    }
}
