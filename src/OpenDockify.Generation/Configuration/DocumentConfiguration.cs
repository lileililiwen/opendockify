using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Generation.Models;

namespace OpenDockify.Generation.Configuration;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.OwnerId)
            .IsRequired();

        builder.Property(d => d.TemplateId)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.SnapshotJson)
            .IsRequired();

        builder.Property(d => d.RenderedText)
            .IsRequired();

        builder.Property(d => d.PdfPath)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.HasIndex(d => d.OwnerId);
        builder.HasIndex(d => d.TemplateId);
        builder.HasIndex(d => d.ParentId);
    }
}
