using Labaik.Domain.Map;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class MapPlaceConfiguration : IEntityTypeConfiguration<MapPlace>
{
    public void Configure(EntityTypeBuilder<MapPlace> builder)
    {
        builder.ToTable("MapPlaces");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Key).HasMaxLength(MapPlace.KeyMaxLength).IsRequired();
        builder.HasIndex(p => p.Key).IsUnique(); // seed identity
        builder.Property(p => p.Name).HasMaxLength(MapPlace.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(MapPlace.DescriptionMaxLength);
        builder.Property(p => p.ImageUrl).HasMaxLength(MapPlace.UrlMaxLength);
        builder.HasIndex(p => new { p.Type, p.SortOrder });
    }
}

internal sealed class EmergencyAlertConfiguration : IEntityTypeConfiguration<EmergencyAlert>
{
    public void Configure(EntityTypeBuilder<EmergencyAlert> builder)
    {
        builder.ToTable("EmergencyAlerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Note).HasMaxLength(MapErrors.NoteMaxLength);
        builder.HasIndex(a => new { a.UserId, a.CreatedAtUtc });
    }
}
