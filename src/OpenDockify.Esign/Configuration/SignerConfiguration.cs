using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Esign.Models;

namespace OpenDockify.Esign.Configuration;

public sealed class SignerConfiguration : IEntityTypeConfiguration<Signer>
{
    public void Configure(EntityTypeBuilder<Signer> builder)
    {
        builder.ToTable("Signers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.DocumentId)
            .IsRequired();

        builder.Property(s => s.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Role)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(s => s.SigningOrder)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.SignedAt);

        builder.HasIndex(s => s.DocumentId);
    }
}
