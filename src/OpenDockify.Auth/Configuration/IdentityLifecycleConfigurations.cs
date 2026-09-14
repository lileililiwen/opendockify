using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Auth.Models;

namespace OpenDockify.Auth.Configuration;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.FamilyId).HasMaxLength(64).IsRequired();
        builder.Property(r => r.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.RevokeReason).HasMaxLength(64);
        builder.Property(r => r.IssuedAt).IsRequired();
        builder.Property(r => r.ExpiresAt).IsRequired();

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.FamilyId);
        builder.HasIndex(r => r.ExpiresAt);
    }
}

public sealed class RecoveryTokenConfiguration : IEntityTypeConfiguration<RecoveryToken>
{
    public void Configure(EntityTypeBuilder<RecoveryToken> builder)
    {
        builder.ToTable("RecoveryTokens");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId);
        builder.Property(r => r.ChallengeId).HasMaxLength(64).IsRequired();
        builder.Property(r => r.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.IssuedAt).IsRequired();
        builder.Property(r => r.ExpiresAt).IsRequired();

        builder.HasIndex(r => r.ChallengeId).IsUnique();
        builder.HasIndex(r => r.ExpiresAt);
    }
}

public sealed class TwoFactorSecretConfiguration : IEntityTypeConfiguration<TwoFactorSecret>
{
    public void Configure(EntityTypeBuilder<TwoFactorSecret> builder)
    {
        builder.ToTable("TwoFactorSecrets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.Secret).HasMaxLength(64).IsRequired();
        builder.Property(t => t.RecoveryCodesHash).HasMaxLength(2048).IsRequired();
        builder.Property(t => t.IsEnabled).IsRequired();

        builder.HasIndex(t => t.UserId).IsUnique();
    }
}

public sealed class TwoFactorChallengeConfiguration : IEntityTypeConfiguration<TwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<TwoFactorChallenge> builder)
    {
        builder.ToTable("TwoFactorChallenges");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.ChallengeId).HasMaxLength(64).IsRequired();
        builder.Property(c => c.IssuedAt).IsRequired();
        builder.Property(c => c.ExpiresAt).IsRequired();

        builder.HasIndex(c => c.ChallengeId).IsUnique();
        builder.HasIndex(c => c.ExpiresAt);
    }
}

public sealed class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("LoginAttempts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Username).HasMaxLength(64).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Succeeded).IsRequired();
        builder.Property(a => a.At).IsRequired();

        builder.HasIndex(a => new { a.Username, a.At });
        builder.HasIndex(a => new { a.IpAddress, a.At });
        builder.HasIndex(a => a.At);
    }
}

public sealed class IdentityAuditEventConfiguration : IEntityTypeConfiguration<IdentityAuditEvent>
{
    public void Configure(EntityTypeBuilder<IdentityAuditEvent> builder)
    {
        builder.ToTable("IdentityAuditEvents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.SubjectId).HasMaxLength(64);
        builder.Property(a => a.Succeeded).IsRequired();
        builder.Property(a => a.OccurredAt).IsRequired();
        builder.Property(a => a.Metadata).HasMaxLength(2048);

        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.SubjectId, a.OccurredAt });
    }
}
