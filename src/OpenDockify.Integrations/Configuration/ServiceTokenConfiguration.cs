using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Integrations.Models;

namespace OpenDockify.Integrations.Configuration;

public sealed class ServiceTokenConfiguration : IEntityTypeConfiguration<ServiceToken>
{
    public void Configure(EntityTypeBuilder<ServiceToken> builder)
    {
        builder.ToTable("ServiceTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(16).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.Prefix).IsUnique();
        builder.HasIndex(x => x.OwnerId);
    }
}
