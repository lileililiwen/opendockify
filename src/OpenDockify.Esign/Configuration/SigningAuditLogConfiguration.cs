using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Esign.Models;

namespace OpenDockify.Esign.Configuration;

public sealed class SigningAuditLogConfiguration : IEntityTypeConfiguration<SigningAuditLog>
{
    public void Configure(EntityTypeBuilder<SigningAuditLog> builder)
    {
        builder.ToTable("SigningAuditLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.DocumentId)
            .IsRequired();

        builder.Property(l => l.Actor)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.Action)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(l => l.Timestamp)
            .IsRequired();

        builder.Property(l => l.Detail)
            .HasMaxLength(2000);

        builder.HasIndex(l => l.DocumentId);
        builder.HasIndex(l => l.Timestamp);
    }
}
