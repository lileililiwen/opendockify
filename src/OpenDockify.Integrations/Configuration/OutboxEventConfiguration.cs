using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Integrations.Models;

namespace OpenDockify.Integrations.Configuration;

public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("OutboxEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasIndex(x => new { x.ProcessedAtUtc, x.CreatedAtUtc });
        builder.HasIndex(x => x.OwnerId);
    }
}
