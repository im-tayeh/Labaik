using Labaik.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class PilgrimProfileConfiguration : IEntityTypeConfiguration<PilgrimProfile>
{
    public void Configure(EntityTypeBuilder<PilgrimProfile> builder)
    {
        builder.ToTable("PilgrimProfiles");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.UserId).IsUnique(); // one profile per user
        builder.Property(p => p.Nationality).HasMaxLength(2);
        builder.Property(p => p.PhotoUrl).HasMaxLength(ProfileErrors.PhotoUrlMaxLength);
        builder.Property(p => p.HealthNotes).HasMaxLength(ProfileErrors.HealthNotesMaxLength); // sensitive
    }
}
