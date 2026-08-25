using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Sharing.Models;

namespace OpenDockify.Sharing.Configuration;

public sealed class DocumentGrantConfiguration : IEntityTypeConfiguration<DocumentGrant>
{
    public void Configure(EntityTypeBuilder<DocumentGrant> builder)
    {
        builder.ToTable("DocumentGrants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessLevel).HasConversion<int>();
        builder.HasIndex(x => new { x.DocumentId, x.GranteeId, x.RevokedAt });
        builder.HasIndex(x => x.OwnerId);
    }
}

public sealed class ExternalShareLinkConfiguration : IEntityTypeConfiguration<ExternalShareLink>
{
    public void Configure(EntityTypeBuilder<ExternalShareLink> builder)
    {
        builder.ToTable("ExternalShareLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SecretHash).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.DocumentId, x.RevokedAt });
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => x.OwnerId);
    }
}

public sealed class ShareAuditEventConfiguration : IEntityTypeConfiguration<ShareAuditEvent>
{
    public void Configure(EntityTypeBuilder<ShareAuditEvent> builder)
    {
        builder.ToTable("ShareAuditEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ActorCategory).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CoarseClient).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.DocumentId, x.CreatedAtUtcTicks });
        builder.HasIndex(x => x.CreatedAtUtcTicks);
    }
}
