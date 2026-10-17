using Labaik.Domain.Companies;
using Labaik.Domain.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(TripErrors.NameMaxLength).IsRequired();
        builder.Property(t => t.DeparturePlace).HasMaxLength(TripErrors.PlaceMaxLength).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(TripErrors.NotesMaxLength);

        // Destinations: a small list of names -> PostgreSQL text[] (primitive collection), via the private field.
        builder.Property<List<string>>("_destinations").HasColumnName("Destinations").IsRequired();
        builder.Ignore(t => t.Destinations);

        // Trips belong to a company (reference by id; no navigation across aggregates).
        builder.HasOne<Company>().WithMany().HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.CompanyId, t.DepartureDate });
    }
}
