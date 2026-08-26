using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Integrations.Models;

namespace OpenDockify.Integrations.Configuration;

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.State).HasConversion<int>().IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(500);
        builder.Property(x => x.BlockedReason).HasMaxLength(100);
        // One delivery per (subscription, event): at-least-once per receiver,
        // never duplicated by outbox re-processing.
        builder.HasIndex(x => new { x.SubscriptionId, x.EventId }).IsUnique();
        builder.HasIndex(x => new { x.State, x.NextAttemptAtUtc });
        builder.HasIndex(x => x.OwnerId);
    }
}
