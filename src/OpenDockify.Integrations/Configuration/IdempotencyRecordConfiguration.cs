using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Integrations.Models;

namespace OpenDockify.Integrations.Configuration;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Route).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RequestDigest).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseJson).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<int>().IsRequired();
        // The unique (token, key) pair is what makes concurrent duplicate
        // mutations resolve to exactly one stored outcome.
        builder.HasIndex(x => new { x.TokenId, x.Key }).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);
        builder.HasIndex(x => x.OwnerId);
    }
}
