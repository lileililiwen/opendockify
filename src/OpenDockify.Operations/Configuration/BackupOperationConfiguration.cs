using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Operations.Models;

namespace OpenDockify.Operations.Configuration;

public sealed class BackupOperationConfiguration : IEntityTypeConfiguration<BackupOperation>
{
    public void Configure(EntityTypeBuilder<BackupOperation> builder)
    {
        builder.ToTable("BackupOperations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.State).HasConversion<int>().IsRequired();
        builder.Property(x => x.BundleDigest).HasMaxLength(64);
        builder.Property(x => x.BundleStorageKey).HasMaxLength(512);
        builder.Property(x => x.Summary).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.StartedAtUtc);
    }
}
