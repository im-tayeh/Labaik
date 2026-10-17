using Labaik.Domain.Announcements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).HasMaxLength(AnnouncementErrors.TitleMaxLength).IsRequired();
        builder.Property(a => a.Body).HasMaxLength(AnnouncementErrors.BodyMaxLength).IsRequired();
        builder.Property(a => a.Location).HasMaxLength(AnnouncementErrors.LocationMaxLength);
        builder.Property(a => a.ImageUrl).HasMaxLength(AnnouncementErrors.ImageUrlMaxLength);
        builder.HasIndex(a => new { a.GroupId, a.CreatedAtUtc });                  // group feed, newest first
        builder.HasIndex(a => new { a.CompanyId, a.Audience, a.CreatedAtUtc });    // company-wide feed

        // Reads are appended to saved announcements -> ValueGeneratedNever so new rows INSERT.
        builder.OwnsMany(a => a.Reads, r =>
        {
            r.ToTable("AnnouncementReads");
            r.WithOwner().HasForeignKey("AnnouncementId");
            r.HasKey(x => x.Id);
            r.Property(x => x.Id).ValueGeneratedNever();
            r.HasIndex("AnnouncementId", nameof(AnnouncementRead.UserId)).IsUnique(); // one read per user
        });

        builder.Navigation(a => a.Reads).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
