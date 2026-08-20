using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.SystemConfig.Models;

namespace OpenDockify.SystemConfig.Configuration;

public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("Settings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(s => s.Key)
            .IsUnique();

        builder.Property(s => s.ValueJson)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();
    }
}
