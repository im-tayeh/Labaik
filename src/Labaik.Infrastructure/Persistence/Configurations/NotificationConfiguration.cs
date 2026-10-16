using Labaik.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Preview).HasMaxLength(Notification.PreviewMaxLength);
        builder.HasIndex(n => new { n.UserId, n.CreatedAtUtc }); // my feed, newest first
        builder.HasIndex(n => new { n.UserId, n.ReadAtUtc });    // unread count
    }
}
