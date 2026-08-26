using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDockify.Integrations.Models;

namespace OpenDockify.Integrations.Configuration;

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("WebhookSubscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Secret).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EventTypes).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.OwnerId);
    }
}
